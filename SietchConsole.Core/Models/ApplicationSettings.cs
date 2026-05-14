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

    // ── Cloud sync settings (#173, #174, #175, #176) ────────────────────────

    /// <summary>Whether cloud sync is active for new backups.</summary>
    public bool CloudSyncEnabled { get; set; } = false;

    /// <summary>"None", "OneDrive", or "S3".</summary>
    public string CloudSyncProvider { get; set; } = "None";

    /// <summary>Remote folder path for OneDrive (e.g. "SietchConsole/Backups").</summary>
    public string? CloudSyncFolderPath { get; set; } = "SietchConsole/Backups";

    // ── S3-compatible settings (#176) ────────────────────────────────────────

    public string? S3BucketName { get; set; }

    /// <summary>AWS region name (e.g. "us-east-1") or S3-compatible region token.</summary>
    public string? S3Region { get; set; } = "us-east-1";

    /// <summary>Custom endpoint URL for non-AWS providers (Backblaze, MinIO, R2). Null = use AWS.</summary>
    public string? S3EndpointUrl { get; set; }

    public string? S3AccessKeyId { get; set; }

    /// <summary>DPAPI-encrypted secret key stored as a Base64 string.</summary>
    public string? S3EncryptedSecretKey { get; set; }

    // ── Discord webhook settings (#170, #171, #172) ──────────────────────────

    /// <summary>Whether Discord webhook notifications are enabled.</summary>
    public bool DiscordWebhookEnabled { get; set; } = false;

    /// <summary>The Discord webhook URL to POST notifications to.</summary>
    public string? DiscordWebhookUrl { get; set; }

    /// <summary>Send a notification when the server reaches the ready state.</summary>
    public bool DiscordNotifyServerStart { get; set; } = true;

    /// <summary>Send a notification when the server is stopped normally.</summary>
    public bool DiscordNotifyServerStop { get; set; } = true;

    /// <summary>Send a notification when the server exits unexpectedly.</summary>
    public bool DiscordNotifyServerCrash { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
