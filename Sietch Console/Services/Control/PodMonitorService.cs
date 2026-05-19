using Microsoft.Extensions.Logging;
using SietchConsole.Core.Interfaces;
using SietchConsole.Core.Models;

namespace Sietch_Console.Services.Control;

/// <summary>
/// Implements <see cref="IServerProcessService"/> against Kubernetes pods running inside the
/// Hyper-V VM rather than a host-side process.  Polls <c>kubectl get pods</c> every 10 seconds
/// to track running/ready state, streams pod logs to <see cref="OutputLineReceived"/>, and fires
/// <see cref="ProcessExited"/> when pods enter a crash or failed state.
/// </summary>
public sealed class PodMonitorService : IServerProcessService, IDisposable
{
    private readonly ISshService              _ssh;
    private readonly ILogger<PodMonitorService> _log;

    private volatile bool _isRunning;
    private volatile bool _isServerReady;
    private string        _namespace = "dune";

    private CancellationTokenSource? _pollCts;
    private CancellationTokenSource? _logCts;
    private Task?                    _pollTask;
    private Task?                    _logTask;

    // ── IServerProcessService ─────────────────────────────────────────────────

    public bool    IsRunning           => _isRunning;
    public bool    IsServerReady       => _isServerReady;
    public int?    LastExitCode        { get; private set; }
    public string? LastExitDescription { get; private set; }

    public event EventHandler<string>?                     OutputLineReceived;
    public event EventHandler?                             ServerStarted;
    public event EventHandler<ServerProcessExitEventArgs>? ProcessExited;

    // ── Constructor ───────────────────────────────────────────────────────────

    public PodMonitorService(ISshService ssh, ILogger<PodMonitorService> log)
    {
        _ssh = ssh;
        _log = log;
    }

    // ── Start ─────────────────────────────────────────────────────────────────

    public Task StartAsync(BattlegroupProfile profile, CancellationToken ct = default)
    {
        if (!_ssh.IsConnected)
            throw new InvalidOperationException(
                "SSH is not connected. Configure SSH key and VM IP in the battlegroup profile, " +
                "then ensure the VM is running before starting the battlegroup.");

        _namespace     = string.IsNullOrWhiteSpace(profile.BattlegroupNamespace)
                         ? "dune"
                         : profile.BattlegroupNamespace;
        _isRunning     = true;
        _isServerReady = false;

        // Kick off background polling and log streaming
        _pollCts  = new CancellationTokenSource();
        _pollTask = PollPodsAsync(_pollCts.Token);

        _logCts  = new CancellationTokenSource();
        _logTask = StreamLogsAsync(_logCts.Token);

        return Task.CompletedTask;
    }

    // ── Stop ──────────────────────────────────────────────────────────────────

    public async Task StopAsync(TimeSpan timeout, CancellationToken ct = default)
    {
        // Cancel background tasks first
        _pollCts?.Cancel();
        _logCts?.Cancel();

        // Scale all deployments to zero
        if (_ssh.IsConnected)
        {
            // NOTE: exact kubectl commands will be refined once battlegroup.bat internals
            // are known.  This scales all Deployments in the namespace to 0 replicas.
            await _ssh.ExecuteAsync(
                $"kubectl -n {_namespace} scale deployment --all --replicas=0 2>/dev/null", ct);
        }

        _isRunning     = false;
        _isServerReady = false;

        try
        {
            if (_pollTask is not null) await _pollTask.WaitAsync(timeout, CancellationToken.None);
            if (_logTask  is not null) await _logTask.WaitAsync(timeout,  CancellationToken.None);
        }
        catch (OperationCanceledException) { }
        catch (TimeoutException)           { }
    }

    // ── Stdin command (N/A for pods) ─────────────────────────────────────────

    public Task SendCommandAsync(string command)
    {
        // Direct stdin is not available for Kubernetes pods.
        // Future: use kubectl exec to run commands inside a specific pod.
        _log.LogWarning("SendCommandAsync is not supported by PodMonitorService (kubectl exec not yet implemented). Command: {Command}", command);
        return Task.CompletedTask;
    }

    // ── Pod status polling ────────────────────────────────────────────────────

    private async Task PollPodsAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        try
        {
            // Initial check without waiting for first tick
            await CheckPodsAsync(ct);
            while (await timer.WaitForNextTickAsync(ct))
                await CheckPodsAsync(ct);
        }
        catch (OperationCanceledException) { }
    }

    private async Task CheckPodsAsync(CancellationToken ct)
    {
        if (!_ssh.IsConnected) return;
        try
        {
            // Returns lines like: "pod-name   Running   true"
            var result = await _ssh.ExecuteAsync(
                $"kubectl -n {_namespace} get pods --no-headers " +
                $"-o custom-columns=" +
                $"NAME:.metadata.name," +
                $"PHASE:.status.phase," +
                $"READY:.status.containerStatuses[0].ready " +
                $"2>/dev/null", ct);

            if (!result.Success || string.IsNullOrWhiteSpace(result.Output))
            {
                // No pods found — possibly still starting or already stopped
                return;
            }

            bool anyReady   = false;
            bool anyCrash   = false;
            bool anyRunning = false;

            foreach (var line in result.Output.Split('\n',
                         StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var phase = parts.Length > 1 ? parts[1] : string.Empty;
                var ready = parts.Length > 2 ? parts[2] : "false";

                if (phase.Equals("Running", StringComparison.OrdinalIgnoreCase))
                {
                    anyRunning = true;
                    if (ready.Equals("true", StringComparison.OrdinalIgnoreCase))
                        anyReady = true;
                }

                if (phase.Equals("Failed", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("CrashLoopBackOff", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("OOMKilled",        StringComparison.OrdinalIgnoreCase))
                {
                    anyCrash = true;
                }
            }

            if (anyCrash && _isRunning)
            {
                FireCrash("One or more battlegroup pods crashed (CrashLoopBackOff / OOMKilled / Failed). Check App Logs for details.");
                return;
            }

            if (anyReady && !_isServerReady)
            {
                _isServerReady = true;
                ServerStarted?.Invoke(this, EventArgs.Empty);
                _log.LogInformation("Battlegroup pods are Ready in namespace '{Namespace}'.", _namespace);
            }
            else if (!anyRunning && _isRunning && _isServerReady)
            {
                // All pods disappeared while we thought things were running
                FireCrash("Battlegroup pods stopped unexpectedly.");
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Pod status poll failed.");
        }
    }

    private void FireCrash(string reason)
    {
        if (!_isRunning) return;
        _isRunning          = false;
        _isServerReady      = false;
        LastExitCode        = 1;
        LastExitDescription = reason;

        ProcessExited?.Invoke(this, new ServerProcessExitEventArgs
        {
            ExitCode    = 1,
            Description = reason,
            WasExpected = false,
        });
    }

    // ── Log streaming ─────────────────────────────────────────────────────────

    private async Task StreamLogsAsync(CancellationToken ct)
    {
        if (!_ssh.IsConnected) return;
        try
        {
            // --prefix includes the pod/container name on each line so the log view
            // shows which server produced each line.
            var cmd = $"kubectl -n {_namespace} logs -f " +
                      $"--all-containers --prefix --ignore-errors 2>/dev/null";

            await foreach (var line in _ssh.StreamLinesAsync(cmd, ct))
                OutputLineReceived?.Invoke(this, line);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (_isRunning)
        {
            _log.LogWarning(ex, "Pod log streaming stopped unexpectedly.");
        }
    }

    // ── Disposal ──────────────────────────────────────────────────────────────

    public void Dispose()
    {
        _pollCts?.Cancel(); _pollCts?.Dispose();
        _logCts?.Cancel();  _logCts?.Dispose();
    }
}
