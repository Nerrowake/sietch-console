namespace SietchConsole.Core.Models;

/// <summary>
/// A point-in-time snapshot of server health collected every 60 seconds (#164).
/// Retained for 30 days; older records are pruned automatically.
/// </summary>
public class ServerMetricSnapshot
{
    public int      Id            { get; set; }
    public string   ProfileId     { get; set; } = string.Empty;
    public DateTime Timestamp     { get; set; } = DateTime.UtcNow;
    public int      PlayerCount   { get; set; }
    public double   CpuPercent    { get; set; }
    public long     MemoryMb      { get; set; }
    public long     UptimeSeconds { get; set; }
}
