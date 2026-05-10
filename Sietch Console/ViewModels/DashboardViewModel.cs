using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using SietchConsole.Core.Exceptions;
using SietchConsole.Core.Interfaces;
using SietchConsole.Core.Models;
using System.Windows;
using System.Windows.Threading;

namespace Sietch_Console.ViewModels;

public enum PendingDashboardAction { None, Stop, Restart, Update }

public partial class DashboardViewModel : ObservableObject
{
    private readonly IBattlegroupControlService _controlService;
    private readonly IServerPackageInstaller    _installer;
    private readonly IServiceScopeFactory       _scopeFactory;
    private readonly DispatcherTimer            _refreshTimer;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProfile))]
    [NotifyPropertyChangedFor(nameof(ProfileName))]
    [NotifyPropertyChangedFor(nameof(VmName))]
    private BattlegroupProfile? _activeProfile;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(StatusDotBrush))]
    [NotifyPropertyChangedFor(nameof(IsRunning))]
    [NotifyPropertyChangedFor(nameof(IsStopped))]
    [NotifyPropertyChangedFor(nameof(HasResourceData))]
    private BattlegroupRuntimeStatus _status = BattlegroupRuntimeStatus.Unknown;

    [ObservableProperty] private bool _isTransitioning;
    [ObservableProperty] private PendingDashboardAction _pendingAction = PendingDashboardAction.None;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _lastError;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasResourceData))]
    private int _cpuPercent;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasResourceData))]
    [NotifyPropertyChangedFor(nameof(MemoryGbText))]
    private long _memoryMb;

    // Update-check state
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUpdateAvailable))]
    [NotifyPropertyChangedFor(nameof(UpdateBannerText))]
    private bool? _isUpdateAvailable;   // null = not yet checked / inconclusive

    [ObservableProperty] private bool   _isUpdating;
    [ObservableProperty] private double _updateProgress;
    [ObservableProperty] private string _updateLog = string.Empty;

    public bool HasProfile      => ActiveProfile is not null;
    public string ProfileName   => ActiveProfile?.Name ?? "No battlegroup configured";
    public string VmName        => ActiveProfile?.VmName ?? "—";
    public bool IsRunning       => Status == BattlegroupRuntimeStatus.Running;
    public bool IsStopped       => Status is BattlegroupRuntimeStatus.Offline or BattlegroupRuntimeStatus.Unknown;
    public bool ShowConfirmation => PendingAction != PendingDashboardAction.None;
    public bool HasError         => LastError is not null;
    public bool HasResourceData  => IsRunning && (CpuPercent > 0 || MemoryMb > 0);
    public bool HasUpdateAvailable => IsUpdateAvailable == true;

    public string UpdateBannerText => IsUpdateAvailable switch
    {
        true  => "A server update is available. Stop the server and update now.",
        false => "Server is up to date.",
        null  => string.Empty,
    };

    public string MemoryGbText => MemoryMb >= 1024
        ? $"{MemoryMb / 1024.0:F1} GB"
        : $"{MemoryMb} MB";

    public string StatusText => Status switch
    {
        BattlegroupRuntimeStatus.Running  => "Running",
        BattlegroupRuntimeStatus.Starting => "Starting…",
        BattlegroupRuntimeStatus.Stopping => "Stopping…",
        BattlegroupRuntimeStatus.Offline  => "Offline",
        BattlegroupRuntimeStatus.Error    => "Error",
        _                                 => "Unknown",
    };

    public string StatusDotBrush => Status switch
    {
        BattlegroupRuntimeStatus.Running  => "StatusSuccess",
        BattlegroupRuntimeStatus.Starting => "StatusRunning",
        BattlegroupRuntimeStatus.Stopping => "StatusWarning",
        BattlegroupRuntimeStatus.Error    => "StatusError",
        _                                 => "StatusOffline",
    };

    public string ConfirmationTitle => PendingAction switch
    {
        PendingDashboardAction.Stop   => "Stop Battlegroup?",
        PendingDashboardAction.Update => "Update Server?",
        _                             => "Restart Battlegroup?",
    };

    public string ConfirmationBody => PendingAction switch
    {
        PendingDashboardAction.Stop   => "This will shut down the server and disconnect all active players.",
        PendingDashboardAction.Update => "The server will be stopped, updated via SteamCMD, and then restarted. All players will be disconnected.",
        _                             => "This will restart the server and briefly disconnect all active players.",
    };

    public DashboardViewModel(
        IBattlegroupControlService controlService,
        IServerPackageInstaller    installer,
        IServiceScopeFactory       scopeFactory,
        IServerProcessService      processService)
    {
        _controlService = controlService;
        _installer      = installer;
        _scopeFactory   = scopeFactory;

        // Subscribe to process exit so unexpected crashes surface immediately (#131)
        processService.ProcessExited += OnServerProcessExited;

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _refreshTimer.Tick += async (_, _) => await RefreshStatusAsync();
        _refreshTimer.Start();
    }

    // Fires on a thread-pool thread — must marshal to Dispatcher before touching UI state.
    private void OnServerProcessExited(object? sender, ServerProcessExitEventArgs e)
    {
        if (e.WasExpected) return;   // intentional stop — handled by RefreshStatusAsync

        Application.Current.Dispatcher.InvokeAsync(() =>
        {
            Status    = e.ExitCode == 0
                ? BattlegroupRuntimeStatus.Offline
                : BattlegroupRuntimeStatus.Error;
            LastError = e.ExitCode == 0 ? null : e.Description;
        });
    }

    public async Task InitializeAsync()
    {
        using var scope  = _scopeFactory.CreateScope();
        var settingsRepo = scope.ServiceProvider.GetRequiredService<IApplicationSettingsRepository>();
        var profileRepo  = scope.ServiceProvider.GetRequiredService<IBattlegroupProfileRepository>();

        var settings = await settingsRepo.GetAsync();
        if (int.TryParse(settings.LastOpenedBattlegroupId, out var id))
            ActiveProfile = await profileRepo.GetByIdAsync(id);
        else
            ActiveProfile = (await profileRepo.GetAllAsync()).FirstOrDefault();

        await RefreshStatusAsync();

        // Check for updates in the background after the UI is ready
        if (ActiveProfile is not null)
            _ = Task.Run(() => CheckForUpdatesAsync());
    }

    private async Task RefreshStatusAsync()
    {
        if (ActiveProfile is null) return;

        try
        {
            Status    = await _controlService.GetStatusAsync(ActiveProfile);
            LastError = null;
        }
        catch (HyperVException ex)
        {
            Status    = BattlegroupRuntimeStatus.Error;
            LastError = ex.UserFacingMessage;
            return;
        }

        if (Status == BattlegroupRuntimeStatus.Running)
        {
            var resources = await _controlService.GetVmResourcesAsync(ActiveProfile);
            if (resources is not null)
            {
                CpuPercent = resources.CpuPercent;
                MemoryMb   = resources.MemoryMb;
            }
        }
        else
        {
            CpuPercent = 0;
            MemoryMb   = 0;
        }
    }

    // #51 – Start
    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync()
    {
        if (ActiveProfile is null) return;
        IsTransitioning = true;
        LastError       = null;
        Status          = BattlegroupRuntimeStatus.Starting;
        try
        {
            await _controlService.StartAsync(ActiveProfile);
        }
        catch (HyperVException ex) { LastError = ex.UserFacingMessage; }
        catch (Exception ex)        { LastError = ex.Message; }
        finally { IsTransitioning = false; await RefreshStatusAsync(); }
    }
    private bool CanStart() => HasProfile && IsStopped && !IsTransitioning && !IsUpdating;

    // #52 – Stop (with confirmation)
    [RelayCommand(CanExecute = nameof(CanStop))]
    private void RequestStop()
    {
        PendingAction = PendingDashboardAction.Stop;
        OnPropertyChanged(nameof(ShowConfirmation));
        OnPropertyChanged(nameof(ConfirmationTitle));
        OnPropertyChanged(nameof(ConfirmationBody));
    }
    private bool CanStop() => HasProfile && IsRunning && !IsTransitioning && !IsUpdating;

    // #53 – Restart (with confirmation)
    [RelayCommand(CanExecute = nameof(CanStop))]
    private void RequestRestart()
    {
        PendingAction = PendingDashboardAction.Restart;
        OnPropertyChanged(nameof(ShowConfirmation));
        OnPropertyChanged(nameof(ConfirmationTitle));
        OnPropertyChanged(nameof(ConfirmationBody));
    }

    // Update – request with confirmation overlay
    [RelayCommand(CanExecute = nameof(CanRequestUpdate))]
    private void RequestUpdate()
    {
        PendingAction = PendingDashboardAction.Update;
        OnPropertyChanged(nameof(ShowConfirmation));
        OnPropertyChanged(nameof(ConfirmationTitle));
        OnPropertyChanged(nameof(ConfirmationBody));
    }
    private bool CanRequestUpdate() => HasProfile && !IsTransitioning && !IsUpdating;

    // Check for updates without installing
    [RelayCommand(CanExecute = nameof(CanRequestUpdate))]
    private async Task CheckForUpdatesAsync()
    {
        if (ActiveProfile is null) return;
        IsUpdateAvailable = await _installer.CheckForUpdateAsync(ActiveProfile.InstallPath);
    }

    // #57 – Confirm / Cancel
    [RelayCommand]
    private async Task ConfirmActionAsync()
    {
        var action = PendingAction;
        PendingAction = PendingDashboardAction.None;
        OnPropertyChanged(nameof(ShowConfirmation));

        if (ActiveProfile is null) return;

        if (action == PendingDashboardAction.Update)
        {
            await ExecuteUpdateAsync();
            return;
        }

        IsTransitioning = true;
        LastError       = null;
        Status          = BattlegroupRuntimeStatus.Stopping;
        try
        {
            if (action == PendingDashboardAction.Stop)
                await _controlService.StopAsync(ActiveProfile);
            else
                await _controlService.RestartAsync(ActiveProfile);
        }
        catch (HyperVException ex) { LastError = ex.UserFacingMessage; }
        catch (Exception ex)        { LastError = ex.Message; }
        finally { IsTransitioning = false; await RefreshStatusAsync(); }
    }

    [RelayCommand]
    private void CancelAction()
    {
        PendingAction = PendingDashboardAction.None;
        OnPropertyChanged(nameof(ShowConfirmation));
    }

    [RelayCommand]
    private void DismissError() => LastError = null;

    // #54 – Control interface
    [RelayCommand(CanExecute = nameof(HasProfile))]
    private void OpenControlInterface()
    {
        if (ActiveProfile is not null)
            _controlService.OpenControlInterface(ActiveProfile);
    }

    // #55 – File browser
    [RelayCommand(CanExecute = nameof(HasProfile))]
    private void OpenFileBrowser()
    {
        if (ActiveProfile is not null)
            _controlService.OpenFileBrowser(ActiveProfile);
    }

    // #56 – VM shell
    [RelayCommand(CanExecute = nameof(HasProfile))]
    private void OpenVmShell()
    {
        if (ActiveProfile is not null)
            _controlService.OpenVmShell(ActiveProfile);
    }

    private async Task ExecuteUpdateAsync()
    {
        if (ActiveProfile is null) return;

        IsUpdating      = true;
        UpdateLog       = string.Empty;
        UpdateProgress  = 0;
        LastError       = null;

        var wasRunning = IsRunning;

        try
        {
            // Stop the server if it's running
            if (wasRunning)
            {
                AppendUpdateLog("Stopping server before update…");
                IsTransitioning = true;
                Status          = BattlegroupRuntimeStatus.Stopping;
                await _controlService.StopAsync(ActiveProfile);
                IsTransitioning = false;
                await RefreshStatusAsync();
            }

            // Run the update via SteamCMD
            AppendUpdateLog("Starting update…");

            var progress = new Progress<double>(p => UpdateProgress = p);

            await _installer.UpdateAsync(
                ActiveProfile.InstallPath,
                progress,
                line => AppendUpdateLog(line));

            // Record the new build ID
            var buildId = _installer.GetInstalledBuildId(ActiveProfile.InstallPath);
            if (buildId is not null)
            {
                using var scope  = _scopeFactory.CreateScope();
                var settingsRepo = scope.ServiceProvider.GetRequiredService<IApplicationSettingsRepository>();
                var settings     = await settingsRepo.GetAsync();
                settings.InstalledBuildId = buildId;
                await settingsRepo.SaveAsync(settings);
            }

            IsUpdateAvailable = false;
            AppendUpdateLog("Update complete.");

            // Restart if the server was running before the update
            if (wasRunning)
            {
                AppendUpdateLog("Restarting server…");
                IsTransitioning = true;
                Status          = BattlegroupRuntimeStatus.Starting;
                await _controlService.StartAsync(ActiveProfile);
                IsTransitioning = false;
            }
        }
        catch (HyperVException ex)
        {
            LastError = ex.UserFacingMessage;
            AppendUpdateLog($"[error] {ex.UserFacingMessage}");
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            AppendUpdateLog($"[error] {ex.Message}");
        }
        finally
        {
            IsUpdating      = false;
            IsTransitioning = false;
            await RefreshStatusAsync();
        }
    }

    private void AppendUpdateLog(string line)
    {
        var current = UpdateLog;
        UpdateLog = string.IsNullOrEmpty(current) ? line : $"{current}\n{line}";
    }
}
