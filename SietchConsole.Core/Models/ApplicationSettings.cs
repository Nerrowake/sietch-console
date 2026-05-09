namespace SietchConsole.Core.Models;

public class ApplicationSettings
{
    public int Id { get; set; }

    public string Theme { get; set; } = "dark";

    public string? InstallPath { get; set; }

    public string? BackupDirectory { get; set; }

    public string? LastOpenedBattlegroupId { get; set; }

    public bool CheckForUpdates { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
