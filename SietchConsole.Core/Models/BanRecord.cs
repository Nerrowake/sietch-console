namespace SietchConsole.Core.Models;

/// <summary>A persistent ban entry stored locally in SQLite (#158).</summary>
public class BanRecord
{
    public int       Id         { get; set; }
    public string    PlayerId   { get; set; } = string.Empty;
    public string    PlayerName { get; set; } = string.Empty;
    public string?   Reason     { get; set; }
    public DateTime  BannedAt   { get; set; } = DateTime.UtcNow;

    /// <summary>Null means permanent ban.</summary>
    public DateTime? ExpiresAt  { get; set; }

    public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value < DateTime.UtcNow;
    public bool IsPermanent => !ExpiresAt.HasValue;
}
