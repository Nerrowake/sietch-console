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

    /// <summary>Number of virtual CPUs assigned when provisioning the VM.</summary>
    public int CpuCount { get; set; } = 4;

    /// <summary>RAM assigned when provisioning the VM, in megabytes.</summary>
    public long MemoryMb { get; set; } = 8192;

    /// <summary>Hyper-V virtual switch name used when provisioning the VM. Defaults to "Default Switch".</summary>
    public string? VirtualSwitchName { get; set; }

    public string? LocalIpAddress { get; set; }

    public string? VmIpAddress { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
