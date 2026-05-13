using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SietchConsole.Core.Interfaces;
using SietchConsole.Core.Models;

namespace Sietch_Console.Services.Metrics;

/// <summary>
/// Collects server health snapshots every 60 seconds while the server is running (#164).
/// Snapshots are written to SQLite via IMetricsRepository and broadcast via SnapshotCollected.
/// Also records downtime events when the server process exits unexpectedly (#163).
/// </summary>
public sealed class MetricsCollectorService : IMetricsCollectorService, IAsyncDisposable
{
    private readonly IServiceScopeFactory            _scopeFactory;
    private readonly IBattlegroupControlService      _control;
    private readonly IServerProcessService           _process;
    private readonly ILogger<MetricsCollectorService> _log;

    private CancellationTokenSource? _cts;
    private Task?                    _collectorTask;
    private BattlegroupProfile?      _currentProfile;
    private int?                     _openDowntimeEventId;
    private DateTime?                _serverStartedAt;

    // ── IMetricsCollectorService ──────────────────────────────────────────────

    public bool IsCollecting => _collectorTask is { IsCompleted: false };

    public event EventHandler<ServerMetricSnapshot>? SnapshotCollected;

    public MetricsCollectorService(
        IServiceScopeFactory       scopeFactory,
        IBattlegroupControlService control,
        IServerProcessService      process,
        ILogger<MetricsCollectorService> log)
    {
        _scopeFactory = scopeFactory;
        _control      = control;
        _process      = process;
        _log          = log;

        _process.ProcessExited += OnProcessExited;
    }

    // ── Start / Stop ──────────────────────────────────────────────────────────

    public async Task StartAsync(BattlegroupProfile profile, CancellationToken ct = default)
    {
        if (IsCollecting) return;

        _currentProfile  = profile;
        _serverStartedAt = DateTime.UtcNow;
        _cts             = CancellationTokenSource.CreateLinkedTokenSource(ct);

        // Close any leftover open downtime event from a previous run.
        await CloseOpenDowntimeEventIfAnyAsync();

        _collectorTask = RunCollectorLoopAsync(_cts.Token);
        _log.LogInformation("Metrics collection started for profile '{Profile}'.", profile.Name);
    }

    public async Task StopAsync()
    {
        if (_cts is null) return;

        await _cts.CancelAsync();
        try
        {
            if (_collectorTask is not null)
                await _collectorTask;
        }
        catch (OperationCanceledException) { }
        finally
        {
            _cts.Dispose();
            _cts           = null;
            _collectorTask = null;
        }

        _log.LogInformation("Metrics collection stopped.");
    }

    // ── Collection loop ───────────────────────────────────────────────────────

    private async Task RunCollectorLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(60));
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                if (_currentProfile is null) continue;
                await CollectSnapshotAsync(_currentProfile, ct);
            }
        }
        catch (OperationCanceledException) { }
    }

    private async Task CollectSnapshotAsync(BattlegroupProfile profile, CancellationToken ct)
    {
        try
        {
            VmResourceSnapshot? resources = null;
            try { resources = await _control.GetVmResourcesAsync(profile); }
            catch { /* VM may be off — metrics will show zeros */ }

            var snapshot = new ServerMetricSnapshot
            {
                ProfileId     = profile.Id.ToString(),
                Timestamp     = DateTime.UtcNow,
                PlayerCount   = 0,   // Populated by PlayerManagementService correlation (future)
                CpuPercent    = resources?.CpuPercent  ?? 0,
                MemoryMb      = resources?.MemoryMb    ?? 0,
                UptimeSeconds = _serverStartedAt.HasValue
                    ? (long)(DateTime.UtcNow - _serverStartedAt.Value).TotalSeconds
                    : 0,
            };

            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IMetricsRepository>();
            await repo.AddSnapshotAsync(snapshot);

            // Prune snapshots older than 30 days.
            await repo.PruneOldSnapshotsAsync(snapshot.ProfileId);

            SnapshotCollected?.Invoke(this, snapshot);

            _log.LogDebug("Snapshot: CPU={Cpu}%, RAM={Ram}MB, Uptime={Up}s",
                snapshot.CpuPercent, snapshot.MemoryMb, snapshot.UptimeSeconds);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to collect metric snapshot.");
        }
    }

    // ── Downtime tracking (#163) ──────────────────────────────────────────────

    private void OnProcessExited(object? sender, ServerProcessExitEventArgs e)
    {
        if (_currentProfile is null) return;

        var reason = e.WasExpected   ? "Manual stop"
                   : e.ExitCode != 0 ? "Crash"
                   :                   "Unknown";

        _ = RecordDowntimeEventAsync(reason);
    }

    private async Task RecordDowntimeEventAsync(string reason)
    {
        if (_currentProfile is null) return;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IMetricsRepository>();

            var evt = new DowntimeEvent
            {
                ProfileId = _currentProfile.Id.ToString(),
                StartedAt = DateTime.UtcNow,
                Reason    = reason,
            };

            _openDowntimeEventId = await repo.OpenDowntimeEventAsync(evt);
            _serverStartedAt     = null;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to open downtime event.");
        }
    }

    private async Task CloseOpenDowntimeEventIfAnyAsync()
    {
        if (_openDowntimeEventId is null || _currentProfile is null) return;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IMetricsRepository>();
            await repo.CloseDowntimeEventAsync(_openDowntimeEventId.Value, DateTime.UtcNow);
            _openDowntimeEventId = null;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to close downtime event {Id}.", _openDowntimeEventId);
        }
    }

    // ── Disposal ──────────────────────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        _process.ProcessExited -= OnProcessExited;
        await StopAsync();
    }
}
