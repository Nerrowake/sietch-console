using CommunityToolkit.Mvvm.ComponentModel;

namespace Sietch_Console.ViewModels.Steps;

public partial class BattlegroupConfigStepViewModel : ObservableObject
{
    public string Title => "Battlegroup";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanProceed))]
    private string _battlegroupName = string.Empty;

    [ObservableProperty]
    private int _serverCount = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanProceed))]
    private string _adminPassword = string.Empty;

    public bool CanProceed =>
        !string.IsNullOrWhiteSpace(BattlegroupName) &&
        !string.IsNullOrWhiteSpace(AdminPassword) &&
        ServerCount is >= 1 and <= 10;
}
