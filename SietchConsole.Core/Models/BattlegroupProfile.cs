namespace SietchConsole.Core.Models;

public class BattlegroupProfile
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string InstallPath { get; set; } = string.Empty;

    public string? VmName { get; set; }

    public string? ServerPackagePath { get; set; }

    public string? UserSettingsPath { get; set; }

    /// <summary>Serialised as string: Offline, Starting, Running, Stopping, Error.</summary>
    public string LastKnownStatus { get; set; } = "Offline";

    public string? LocalIpAddress { get; set; }

    public string? VmIpAddress { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
