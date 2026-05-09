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
}
