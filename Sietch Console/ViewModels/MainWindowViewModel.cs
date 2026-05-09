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

        NavigationItems = new ObservableCollection<NavigationItem>
        {
            new("Dashboard",    dashboard,   "IconDashboard"),
            new("Setup Wizard", setupWizard, "IconSetupWizard"),
            new("Logs",         logs,        "IconLogs"),
            new("Diagnostics",  diagnostics, "IconDiagnostics"),
            new("Backups",      backups,     "IconBackups"),
            new("Networking",   networking,  "IconNetworking"),
            new("Settings",     settings,    "IconSettings"),
        };

        SelectedNavigationItem = NavigationItems[0];
    }

    partial void OnSelectedNavigationItemChanged(NavigationItem? value)
    {
        CurrentViewModel = value?.ViewModel;
    }
}
