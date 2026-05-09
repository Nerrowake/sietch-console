using CommunityToolkit.Mvvm.ComponentModel;

namespace Sietch_Console.ViewModels.Steps;

public partial class ReviewStepViewModel : ObservableObject
{
    public string Title => "Review";
    public bool CanProceed => true;

    // Populated by SetupWizardViewModel before showing this step
    public string InstallPath { get; set; } = string.Empty;
    public string BackupPath { get; set; } = string.Empty;
    public string LogsPath { get; set; } = string.Empty;
    public string VmName { get; set; } = string.Empty;
    public string VmMemory { get; set; } = string.Empty;
    public string NetworkAdapter { get; set; } = string.Empty;
    public string BattlegroupName { get; set; } = string.Empty;
    public int ServerCount { get; set; }
}
