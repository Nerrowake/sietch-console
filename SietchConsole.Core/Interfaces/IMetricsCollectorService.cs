using SietchConsole.Core.Models;

namespace SietchConsole.Core.Interfaces;

/// <summary>
/// Collects periodic server health snapshots while the server is running (#164).
/// Snapshots are written to SQLite and broadcast via <see cref="SnapshotCollected"/>.
/// </summary>
public interface IMetricsCollectorService
{
    bool IsCollecting { get; }

    /// <summary>Raised each time a new snapshot is persisted.</summary>
    event EventHandler<ServerMetricSnapshot>? SnapshotCollected;

    Task StartAsync(BattlegroupProfile profile, CancellationToken ct = default);
    Task StopAsync();
}
