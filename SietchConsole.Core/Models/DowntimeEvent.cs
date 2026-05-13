namespace SietchConsole.Core.Models;

/// <summary>
/// Records a server downtime period for uptime tracking (#163).
/// Reason is inferred from the server exit: "Crash", "Manual stop", or "Unknown".
/// </summary>
public class DowntimeEvent
{
    public int       Id        { get; set; }
    public string    ProfileId { get; set; } = string.Empty;
    public DateTime  StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt   { get; set; }
    public string    Reason    { get; set; } = "Unknown";

    public TimeSpan? Duration  => EndedAt.HasValue ? EndedAt.Value - StartedAt : null;
    public bool      IsOpen    => !EndedAt.HasValue;
}
