using CommunityToolkit.Mvvm.ComponentModel;
using System.Net.NetworkInformation;

namespace Sietch_Console.ViewModels.Steps;

public partial class VmConfigStepViewModel : ObservableObject
{
    public string Title => "VM Setup";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanProceed))]
    private string _vmName = "SietchBattlegroup";

    [ObservableProperty]
    private int _vmMemoryMb = 4096;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanProceed))]
    private string? _selectedAdapterName;

    public IReadOnlyList<string> NetworkAdapters { get; }

    public string VmMemoryDisplay => $"{VmMemoryMb / 1024.0:F1} GB";

    public bool CanProceed =>
        !string.IsNullOrWhiteSpace(VmName) &&
        !string.IsNullOrWhiteSpace(SelectedAdapterName);

    public VmConfigStepViewModel()
    {
        NetworkAdapters = NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up
                     && n.NetworkInterfaceType != NetworkInterfaceType.Loopback
                     && n.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
            .Select(n => n.Name)
            .ToList();

        SelectedAdapterName = NetworkAdapters.FirstOrDefault();
    }

    partial void OnVmMemoryMbChanged(int value)
        => OnPropertyChanged(nameof(VmMemoryDisplay));
}
