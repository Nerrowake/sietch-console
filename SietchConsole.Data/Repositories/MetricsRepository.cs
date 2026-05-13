using Microsoft.EntityFrameworkCore;
using SietchConsole.Core.Interfaces;
using SietchConsole.Core.Models;
using SietchConsole.Data.Database;

namespace SietchConsole.Data.Repositories;

public class MetricsRepository : IMetricsRepository
{
    private readonly SietchConsoleDbContext _db;

    public MetricsRepository(SietchConsoleDbContext db) => _db = db;

    // ── Snapshots ────────────────────────────────────────────────────────────

    public async Task AddSnapshotAsync(ServerMetricSnapshot snapshot)
    {
        _db.MetricSnapshots.Add(snapshot);
        await _db.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<ServerMetricSnapshot>> GetSnapshotsAsync(
        string profileId, DateTime from, DateTime to)
        => await _db.MetricSnapshots
                    .Where(s => s.ProfileId == profileId
                             && s.Timestamp >= from
                             && s.Timestamp <= to)
                    .OrderBy(s => s.Timestamp)
                    .ToListAsync();

    public async Task PruneOldSnapshotsAsync(string profileId, int keepDays = 30)
    {
        var cutoff = DateTime.UtcNow.AddDays(-keepDays);
        var old = await _db.MetricSnapshots
                           .Where(s => s.ProfileId == profileId && s.Timestamp < cutoff)
                           .ToListAsync();

        if (old.Count > 0)
        {
            _db.MetricSnapshots.RemoveRange(old);
            await _db.SaveChangesAsync();
        }
    }

    // ── Downtime events ──────────────────────────────────────────────────────

    public async Task<int> OpenDowntimeEventAsync(DowntimeEvent evt)
    {
        _db.DowntimeEvents.Add(evt);
        await _db.SaveChangesAsync();
        return evt.Id;
    }

    public async Task CloseDowntimeEventAsync(int eventId, DateTime endedAt)
    {
        var evt = await _db.DowntimeEvents.FindAsync(eventId);
        if (evt is not null)
        {
            evt.EndedAt = endedAt;
            await _db.SaveChangesAsync();
        }
    }

    public async Task<IReadOnlyList<DowntimeEvent>> GetDowntimeEventsAsync(
        string profileId, DateTime from, DateTime to)
        => await _db.DowntimeEvents
                    .Where(e => e.ProfileId == profileId
                             && e.StartedAt >= from
                             && e.StartedAt <= to)
                    .OrderByDescending(e => e.StartedAt)
                    .ToListAsync();
}
