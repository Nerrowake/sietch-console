namespace SietchConsole.Core.Models;

public class ApplicationSettings
{
    public int Id { get; set; }

    public string Theme { get; set; } = "dark";

    public string? InstallPath { get; set; }

    public string? BackupDirectory { get; set; }

    public string? LastOpenedBattlegroupId { get; set; }

    public bool CheckForUpdates { get; set; } = true;

    /// <summary>SteamCMD build ID recorded after the last successful install or update.</summary>
    public string? InstalledBuildId { get; set; }

    // ── Auto-backup settings (#136, #138) ────────────────────────────────────

    /// <summary>Whether the automatic scheduled backup timer is active.</summary>
    public bool AutoBackupEnabled { get; set; } = false;

    /// <summary>How often to create an automatic backup, in hours. 0 = disabled.</summary>
    public int BackupIntervalHours { get; set; } = 6;

    /// <summary>Maximum number of backups to keep per profile. Older ones are pruned automatically.</summary>
    public int BackupRetainCount { get; set; } = 10;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
