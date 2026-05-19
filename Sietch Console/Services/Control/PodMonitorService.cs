using Microsoft.Extensions.Logging;
using SietchConsole.Core.Interfaces;
using SietchConsole.Core.Models;

namespace Sietch_Console.Services.Control;

/// <summary>
/// Implements <see cref="IServerProcessService"/> by delegating lifecycle operations to the
/// <c>/home/dune/.dune/bin/battlegroup</c> management binary inside the Hyper-V VM over SSH.
/// <para>
/// On <see cref="StartAsync"/>: issues <c>battlegroup start</c> then polls <c>battlegroup status</c>
/// every 10 s to detect when pods are ready.  Log lines are streamed via <c>battlegroup logs</c>.
/// On <see cref="StopAsync"/>: issues <c>battlegroup stop</c> and cancels background tasks.
/// </para>
/// </summary>
public sealed class PodMonitorService : IServerProcessService, IDisposable
{
    private readonly ISshService                _ssh;
    private readonly ILogger<PodMonitorService> _log;

    // Path to the management binary inside the VM (always at this location after initial-setup)
    private const string BattlegroupBin = "/home/dune/.dune/bin/battlegroup";

    private volatile bool _isRunning;
    private volatile bool _isServerReady;

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
                "SSH is not connected. Configure your SSH key and VM IP in the battlegroup " +
                "profile settings, then ensure the VM is running before starting the battlegroup.");

        _isRunning     = true;
        _isServerReady = false;

        // Issue 'battlegroup start' then poll status in the background.
        // A separate task streams log output concurrently.
        _pollCts  = new CancellationTokenSource();
        _pollTask = StartAndPollAsync(_pollCts.Token);

        _logCts  = new CancellationTokenSource();
        _logTask = StreamLogsAsync(_logCts.Token);

        return Task.CompletedTask;
    }

    // ── Stop ──────────────────────────────────────────────────────────────────

    public async Task StopAsync(TimeSpan timeout, CancellationToken ct = default)
    {
        // Signal background tasks to stop
        _pollCts?.Cancel();
        _logCts?.Cancel();

        // Issue 'battlegroup stop' — use CancellationToken.None so the stop command
        // is never aborted mid-flight even if the caller's token is already cancelled.
        if (_ssh.IsConnected)
        {
            var result = await _ssh.ExecuteAsync($"{BattlegroupBin} stop", CancellationToken.None);
            if (!result.Success)
                _log.LogWarning(
                    "battlegroup stop exited {Code}: {Error}",
                    result.ExitCode, result.Error.Trim());
            else
                _log.LogInformation("battlegroup stop issued successfully.");
        }

        _isRunning     = false;
        _isServerReady = false;

        // Wait for background tasks to drain
        try
        {
            if (_pollTask is not null) await _pollTask.WaitAsync(timeout, CancellationToken.None);
            if (_logTask  is not null) await _logTask.WaitAsync(timeout,  CancellationToken.None);
        }
        catch (OperationCanceledException) { }
        catch (TimeoutException)           { }
    }

    // ── Stdin command ─────────────────────────────────────────────────────────

    public Task SendCommandAsync(string command)
    {
        // Future: use 'battlegroup' subcommands (kick, ban, etc.) when Funcom exposes them.
        _log.LogWarning(
            "SendCommandAsync is not yet implemented for pod-managed battlegroups. Command: {Command}",
            command);
        return Task.CompletedTask;
    }

    // ── Start + poll pipeline ─────────────────────────────────────────────────

    private async Task StartAndPollAsync(CancellationToken ct)
    {
        // 1. Issue 'battlegroup start' — the binary kicks off the k8s pods and returns.
        if (_ssh.IsConnected)
        {
            _log.LogInformation("Issuing: {Bin} start", BattlegroupBin);
            var result = await _ssh.ExecuteAsync($"{BattlegroupBin} start", ct);

            if (result.ExitCode < 0)
            {
                // SSH-level failure (no connection) — abort
                FireCrash("SSH connection failed while issuing 'battlegroup start'.");
                return;
            }

            if (!result.Success)
                _log.LogWarning(
                    "battlegroup start exited {Code}: {Error}",
                    result.ExitCode, result.Error.Trim());
            else
                _log.LogInformation(
                    "battlegroup start returned. Output: {Output}",
                    result.Output.Trim());
        }

        // 2. Poll 'battlegroup status' every 10 s until ready or cancelled.
        await PollStatusAsync(ct);
    }

    private async Task PollStatusAsync(CancellationToken ct)
    {
        // Brief initial delay — give the battlegroup a moment to initialise before
        // the first status query so we don't get a spurious "not running" response.
        await Task.Delay(TimeSpan.FromSeconds(8), ct).ConfigureAwait(false);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        try
        {
            await CheckStatusAsync(ct);
            while (await timer.WaitForNextTickAsync(ct))
                await CheckStatusAsync(ct);
        }
        catch (OperationCanceledException) { }
    }

    private async Task CheckStatusAsync(CancellationToken ct)
    {
        if (!_ssh.IsConnected) return;
        try
        {
            var result = await _ssh.ExecuteAsync($"{BattlegroupBin} status", ct);

            if (result.ExitCode < 0)
            {
                // SSH transport error — skip this tick, don't treat as crash
                _log.LogWarning("battlegroup status could not be reached (SSH error).");
                return;
            }

            // Interpret exit code as the primary signal:
            //   0  = battlegroup is running / healthy
            //  !0  = battlegroup is stopped, starting, or in an error state
            //
            // We also scan stdout+stderr for plain-language keywords as a secondary signal
            // because the binary's exact exit-code semantics are not documented.
            var combinedOutput = (result.Output + " " + result.Error).ToLowerInvariant();

            bool indicatesRunning = result.Success
                                 || combinedOutput.Contains("running")
                                 || combinedOutput.Contains("ready")
                                 || combinedOutput.Contains("started");

            bool indicatesCrash  = combinedOutput.Contains("crashloopbackoff")
                                 || combinedOutput.Contains("oomkilled")
                                 || (combinedOutput.Contains("failed") && !result.Success)
                                 || combinedOutput.Contains("error") && result.ExitCode > 1;

            if (indicatesCrash && _isRunning)
            {
                FireCrash(
                    "Battlegroup reported a failed or crashed state. " +
                    "Check App Logs for details.");
                return;
            }

            if (indicatesRunning && !_isServerReady)
            {
                _isServerReady = true;
                ServerStarted?.Invoke(this, EventArgs.Empty);
                _log.LogInformation("Battlegroup is running (status: OK).");
            }
            else if (!indicatesRunning && _isRunning && _isServerReady)
            {
                // Was healthy on the previous poll — something stopped it.
                FireCrash("Battlegroup stopped unexpectedly (status no longer healthy).");
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Battlegroup status poll failed.");
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
            // 'battlegroup logs' streams the aggregated pod output in real time.
            await foreach (var line in _ssh.StreamLinesAsync($"{BattlegroupBin} logs", ct))
                OutputLineReceived?.Invoke(this, line);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (_isRunning)
        {
            _log.LogWarning(ex, "Battlegroup log streaming stopped unexpectedly.");
        }
    }

    // ── Disposal ──────────────────────────────────────────────────────────────

    public void Dispose()
    {
        _pollCts?.Cancel(); _pollCts?.Dispose();
        _logCts?.Cancel();  _logCts?.Dispose();
    }
}
