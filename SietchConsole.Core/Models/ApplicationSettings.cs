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

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
