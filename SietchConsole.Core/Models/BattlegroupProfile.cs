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

    /// <summary>RAM assigned when provisioning the VM, in megabytes (minimum 20 480 per Funcom requirements).</summary>
    public long MemoryMb { get; set; } = 20480;

    /// <summary>Hyper-V virtual switch name used when provisioning the VM. Defaults to "Default Switch".</summary>
    public string? VirtualSwitchName { get; set; }

    public string? LocalIpAddress { get; set; }

    public string? VmIpAddress { get; set; }

    /// <summary>
    /// The ID of the Hyper-V host this profile targets (#153).
    /// Null means the local machine ("This machine").
    /// </summary>
    public string? HostId { get; set; }

    // ── M28: SSH + battlegroup connection (#177, #178) ────────────────────────

    /// <summary>Path to the SSH private key generated during battlegroup initial-setup.</summary>
    public string? VmSshKeyPath { get; set; }

    /// <summary>SSH username inside the Hyper-V VM (default: "dune").</summary>
    public string VmUsername { get; set; } = "dune";

    /// <summary>SSH port for the Hyper-V VM (default: 22).</summary>
    public int VmSshPort { get; set; } = 22;

    /// <summary>Kubernetes namespace the battlegroup pods run in (default: "dune").</summary>
    public string BattlegroupNamespace { get; set; } = "dune";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
