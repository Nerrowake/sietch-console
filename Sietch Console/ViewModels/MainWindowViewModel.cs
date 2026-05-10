using System.Collections.ObjectModel;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SietchConsole.Core.Interfaces;
using SietchConsole.Core.Models;
using System.Windows;

namespace Sietch_Console.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly IActiveProfileService _activeProfileService;
    private readonly IAppUpdateService     _appUpdateService;

    public ObservableCollection<NavigationItem> NavigationItems { get; }

    [ObservableProperty]
    private NavigationItem? _selectedNavigationItem;

    [ObservableProperty]
    private object? _currentViewModel;

    // Exposed for status-bar binding in MainWindow.xaml (#91, #98)
    public DashboardViewModel Dashboard { get; }

    // ── Version (#M19) ────────────────────────────────────────────────

    /// <summary>Application version string read from AssemblyInformationalVersion.</summary>
    public string AppVersion { get; } =
        "v" + (Assembly.GetEntryAssembly()
                       ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                       ?.InformationalVersion ?? string.Empty);

    // ── Profile switcher (#139) ───────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanDeleteProfile))]
    private BattlegroupProfile? _selectedProfile;

    public ObservableCollection<BattlegroupProfile> AllProfiles { get; } = [];

    public bool CanDeleteProfile => AllProfiles.Count > 1 && SelectedProfile is not null;

    // New-profile creation overlay
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanCreateProfile))]
    private bool _showNewProfileOverlay;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanCreateProfile))]
    private string _newProfileName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanCreateProfile))]
    private string _newProfileInstallPath = string.Empty;

    [ObservableProperty] private bool   _isProfileSwitching;
    [ObservableProperty] private string _profileStatusMessage = string.Empty;

    public bool CanCreateProfile =>
        !string.IsNullOrWhiteSpace(NewProfileName) &&
        !string.IsNullOrWhiteSpace(NewProfileInstallPath);

    // ── App self-update (#145–#148) ───────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowUpdateBanner))]
    private AppUpdateInfo? _pendingUpdate;

    [ObservableProperty] private bool   _isDownloadingUpdate;
    [ObservableProperty] private double _updateDownloadProgress;
    [ObservableProperty] private string _updateStatusMessage = string.Empty;

    public bool ShowUpdateBanner => PendingUpdate is not null;

    // ── Constructor ───────────────────────────────────────────────────

    public MainWindowViewModel(
        DashboardViewModel    dashboard,
        SetupWizardViewModel  setupWizard,
        LogsViewModel         logs,
        DiagnosticsViewModel  diagnostics,
        BackupsViewModel      backups,
        NetworkingViewModel   networking,
        SettingsViewModel     settings,
        AppLogsViewModel      appLogs,
        IActiveProfileService activeProfileService,
        IAppUpdateService     appUpdateService)
    {
        Dashboard             = dashboard;
        _activeProfileService = activeProfileService;
        _appUpdateService     = appUpdateService;

        NavigationItems = new ObservableCollection<NavigationItem>
        {
            new("Dashboard",    dashboard,   "IconDashboard"),
            new("Setup Wizard", setupWizard, "IconSetupWizard"),
            new("Logs",         logs,        "IconLogs"),
            new("Diagnostics",  diagnostics, "IconDiagnostics"),
            new("Backups",      backups,     "IconBackups"),
            new("Networking",   networking,  "IconNetworking"),
            new("Settings",     settings,    "IconSettings"),
            new("App Logs",     appLogs,     "IconLogs"),
        };

        SelectedNavigationItem = NavigationItems[0];

        // Populate profile list and track the active one
        foreach (var p in _activeProfileService.AllProfiles)
            AllProfiles.Add(p);

        SelectedProfile = _activeProfileService.Current;

        _activeProfileService.ProfileChanged += OnActiveProfileChanged;

        // #145 – Check for updates in the background after startup
        _ = Task.Run(CheckForAppUpdateAsync);
    }

    // ── App update methods (#145–#148) ────────────────────────────────────────

    private async Task CheckForAppUpdateAsync()
    {
        var info = await _appUpdateService.CheckForUpdateAsync();
        if (info is not null)
        {
            _ = Application.Current.Dispatcher.InvokeAsync(() =>
            {
                PendingUpdate       = info;
                UpdateStatusMessage = $"{info.TagName} is available.";
            });
        }
    }

    [RelayCommand]
    private async Task DownloadAndInstallUpdateAsync()
    {
        if (PendingUpdate is null) return;

        IsDownloadingUpdate   = true;
        UpdateStatusMessage   = "Downloading installer…";
        UpdateDownloadProgress = 0;
        try
        {
            var progress = new Progress<double>(p =>
                Application.Current.Dispatcher.InvokeAsync(
                    () => UpdateDownloadProgress = p));

            var path = await _appUpdateService.DownloadInstallerAsync(PendingUpdate, progress);
            UpdateStatusMessage = "Download complete. Launching installer…";
            await Task.Delay(600); // let the message show
            _appUpdateService.LaunchInstallerAndExit(path);
        }
        catch (Exception ex)
        {
            UpdateStatusMessage   = $"Download failed: {ex.Message}";
            IsDownloadingUpdate   = false;
        }
    }

    [RelayCommand]
    private void DismissUpdate()
    {
        PendingUpdate       = null;
        UpdateStatusMessage = string.Empty;
    }

    partial void OnSelectedNavigationItemChanged(NavigationItem? value)
        => CurrentViewModel = value?.ViewModel;

    // ── Profile switcher reactions ────────────────────────────────────

    private void OnActiveProfileChanged(object? sender, BattlegroupProfile? profile)
    {
        SelectedProfile = profile;
        RebuildProfileList();
    }

    private void RebuildProfileList()
    {
        AllProfiles.Clear();
        foreach (var p in _activeProfileService.AllProfiles)
            AllProfiles.Add(p);
        OnPropertyChanged(nameof(CanDeleteProfile));
    }

    // Called from the profile ComboBox SelectedItem binding (two-way)
    partial void OnSelectedProfileChanged(BattlegroupProfile? value)
    {
        if (value is null || value.Id == _activeProfileService.Current?.Id) return;
        _ = SwitchProfileAsync(value);
    }

    private async Task SwitchProfileAsync(BattlegroupProfile profile)
    {
        IsProfileSwitching = true;
        try
        {
            await _activeProfileService.SwitchToAsync(profile);
        }
        finally
        {
            IsProfileSwitching = false;
        }
    }

    // ── Profile commands (#139) ───────────────────────────────────────

    [RelayCommand]
    private void OpenNewProfileOverlay()
    {
        NewProfileName        = string.Empty;
        NewProfileInstallPath = string.Empty;
        ProfileStatusMessage  = string.Empty;
        ShowNewProfileOverlay = true;
    }

    [RelayCommand]
    private void CloseNewProfileOverlay()
    {
        ShowNewProfileOverlay = false;
        ProfileStatusMessage  = string.Empty;
    }

    [RelayCommand(CanExecute = nameof(CanCreateProfile))]
    private async Task CreateProfileAsync()
    {
        IsProfileSwitching = true;
        ProfileStatusMessage = string.Empty;
        try
        {
            var profile = await _activeProfileService.CreateAsync(
                NewProfileName.Trim(), NewProfileInstallPath.Trim());

            RebuildProfileList();
            ShowNewProfileOverlay = false;
            ProfileStatusMessage  = $"Profile \"{profile.Name}\" created and activated.";
        }
        catch (Exception ex)
        {
            ProfileStatusMessage = $"Failed to create profile: {ex.Message}";
        }
        finally
        {
            IsProfileSwitching = false;
        }
    }

    [RelayCommand]
    private async Task DeleteProfileAsync()
    {
        if (SelectedProfile is null) return;
        IsProfileSwitching = true;
        ProfileStatusMessage = string.Empty;
        try
        {
            var name = SelectedProfile.Name;
            await _activeProfileService.DeleteAsync(SelectedProfile);
            RebuildProfileList();
            ProfileStatusMessage = $"Profile \"{name}\" deleted.";
        }
        catch (Exception ex)
        {
            ProfileStatusMessage = $"Failed to delete profile: {ex.Message}";
        }
        finally
        {
            IsProfileSwitching = false;
        }
    }
}
