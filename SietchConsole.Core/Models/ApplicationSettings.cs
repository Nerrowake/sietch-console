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

    // ── Remote management settings (#152, #151) ──────────────────────────────

    /// <summary>Whether the embedded Kestrel web server is enabled.</summary>
    public bool RemoteManagementEnabled { get; set; } = false;

    /// <summary>Port the embedded web server listens on (default 5151).</summary>
    public int RemoteManagementPort { get; set; } = 5151;

    /// <summary>Bearer access token for the remote management API (DPAPI-encrypted at rest).</summary>
    public string? RemoteManagementToken { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
