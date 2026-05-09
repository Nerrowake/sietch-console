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

    // Exposed for status-bar binding in MainWindow.xaml (#91, #98)
    public DashboardViewModel Dashboard { get; }

    public MainWindowViewModel(
        DashboardViewModel dashboard,
        SetupWizardViewModel setupWizard,
        LogsViewModel logs,
        DiagnosticsViewModel diagnostics,
        BackupsViewModel backups,
        NetworkingViewModel networking,
        SettingsViewModel settings)
    {
        Dashboard = dashboard;

        // Segoe MDL2 Assets glyphs (#97)
        NavigationItems = new ObservableCollection<NavigationItem>
        {
            new("Dashboard",    dashboard,   ""),  // Home
            new("Setup Wizard", setupWizard, ""),  // PageList
            new("Logs",         logs,        ""),  // EventLog
            new("Diagnostics",  diagnostics, ""),  // HealthSolid
            new("Backups",      backups,     ""),  // BackupDrive
            new("Networking",   networking,  ""),  // NetworkTower
            new("Settings",     settings,    ""),  // Settings gear
        };

        SelectedNavigationItem = NavigationItems[0];
    }

    partial void OnSelectedNavigationItemChanged(NavigationItem? value)
    {
        CurrentViewModel = value?.ViewModel;
    }
}
