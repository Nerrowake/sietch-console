using SietchConsole.Core.Models;

namespace SietchConsole.Core.Interfaces;

/// <summary>Persistence for metric snapshots and downtime events (#163, #164).</summary>
public interface IMetricsRepository
{
    // ── Snapshots ────────────────────────────────────────────────────────────

    Task AddSnapshotAsync(ServerMetricSnapshot snapshot);

    Task<IReadOnlyList<ServerMetricSnapshot>> GetSnapshotsAsync(
        string profileId, DateTime from, DateTime to);

    /// <summary>Deletes snapshots older than <paramref name="keepDays"/> days.</summary>
    Task PruneOldSnapshotsAsync(string profileId, int keepDays = 30);

    // ── Downtime events ──────────────────────────────────────────────────────

    Task<int> OpenDowntimeEventAsync(DowntimeEvent evt);

    Task CloseDowntimeEventAsync(int eventId, DateTime endedAt);

    Task<IReadOnlyList<DowntimeEvent>> GetDowntimeEventsAsync(
        string profileId, DateTime from, DateTime to);
}
