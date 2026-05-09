namespace SietchConsole.Core.Models;

public class SetupWizardState
{
    public int Id { get; set; }
    public int CurrentStepIndex { get; set; }
    public bool IsComplete { get; set; }

    // Step 3 – Install paths
    public string? InstallPath { get; set; }
    public string? BackupPath { get; set; }
    public string? LogsPath { get; set; }

    // Step 4 – Account token (plaintext; encrypt before shipping)
    public string? DuneAccountToken { get; set; }

    // Step 5 – VM configuration
    public string? VmName { get; set; }
    public int VmMemoryMb { get; set; } = 4096;
    public string? NetworkAdapterName { get; set; }

    // Step 6 – Battlegroup configuration
    public string? BattlegroupName { get; set; }
    public int ServerCount { get; set; } = 1;
    public string? AdminPassword { get; set; }

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
