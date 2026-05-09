using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace Sietch_Console.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    public ObservableCollection<NavigationItem> NavigationItems { get; }

    [ObservableProperty]
    private NavigationItem? _selectedNavigationItem;

    [ObservableProperty]
    private object? _currentViewModel;

    public MainWindowViewModel(
        DashboardViewModel dashboard,
        SetupWizardViewModel setupWizard,
        LogsViewModel logs,
        DiagnosticsViewModel diagnostics,
        BackupsViewModel backups,
        NetworkingViewModel networking,
        SettingsViewModel settings)
    {
        NavigationItems = new ObservableCollection<NavigationItem>
        {
            new("Dashboard",     dashboard),
            new("Setup Wizard",  setupWizard),
            new("Logs",          logs),
            new("Diagnostics",   diagnostics),
            new("Backups",       backups),
            new("Networking",    networking),
            new("Settings",      settings),
        };

        SelectedNavigationItem = NavigationItems[0];
    }

    partial void OnSelectedNavigationItemChanged(NavigationItem? value)
    {
        CurrentViewModel = value?.ViewModel;
    }
}
