namespace SietchConsole.Core.Models;

public class BackupRecord
{
    public int Id { get; set; }

    /// <summary>Configuration, SaveData, or Full.</summary>
    public string BackupType { get; set; } = string.Empty;

    public string BackupPath { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public string? Notes { get; set; }

    public string? AppVersion { get; set; }

    public int? BattlegroupProfileId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ── Cloud sync state (#174) ───────────────────────────────────────────────

    /// <summary>UTC timestamp of the last successful cloud upload; null if never uploaded.</summary>
    public DateTime? CloudSyncedAt { get; set; }

    /// <summary>Provider-specific remote file identifier assigned after upload.</summary>
    public string? CloudRemoteId { get; set; }
}
