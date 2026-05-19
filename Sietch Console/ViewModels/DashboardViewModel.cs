using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using SietchConsole.Core.Exceptions;
using SietchConsole.Core.Interfaces;
using SietchConsole.Core.Models;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;

namespace Sietch_Console.ViewModels;

public enum PendingDashboardAction { None, Stop, Restart, Update }

public partial class DashboardViewModel : ObservableObject
{
    private readonly IBattlegroupControlService _controlService;
    private readonly IServiceScopeFactory       _scopeFactory;
    private readonly IActiveProfileService      _activeProfileService;
    private readonly IDiscordWebhookService     _discordService;
    private readonly DispatcherTimer            _refreshTimer;

    private DateTime? _serverStartedAt;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProfile))]
    [NotifyPropertyChangedFor(nameof(ProfileName))]
    [NotifyPropertyChangedFor(nameof(VmName))]
    [NotifyPropertyChangedFor(nameof(VmIpText))]
    [NotifyPropertyChangedFor(nameof(HasVmIp))]
    [NotifyPropertyChangedFor(nameof(UptimeText))]
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

    // Update state
    [ObservableProperty] private bool   _isUpdating;
    [ObservableProperty] private double _updateProgress;
    [ObservableProperty] private string _updateLog = string.Empty;

    // ── Experimental swap (#M29) ──────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SwapStatusMessage))]
    private bool _isEnablingSwap;

    [ObservableProperty] private string _swapResult = string.Empty;

    public string SwapStatusMessage => IsEnablingSwap ? "Enabling swap memory…" : SwapResult;

    // ── Discord announcement (#171) ───────────────────────────────────────────
    [ObservableProperty] private string _announcementText          = string.Empty;
    [ObservableProperty] private string _announcementStatusMessage = string.Empty;
    [ObservableProperty] private bool   _isAnnouncementBusy;

    public bool HasProfile      => ActiveProfile is not null;
    public string ProfileName   => ActiveProfile?.Name ?? "No battlegroup configured";
    public string VmName        => ActiveProfile?.VmName ?? "—";
    public bool IsRunning       => Status == BattlegroupRuntimeStatus.Running;
    public bool IsStopped       => Status is BattlegroupRuntimeStatus.Offline or BattlegroupRuntimeStatus.Unknown;
    public bool ShowConfirmation => PendingAction != PendingDashboardAction.None;
    public bool HasError         => LastError is not null;
    public bool HasResourceData  => IsRunning && (CpuPercent > 0 || MemoryMb > 0);

    public string MemoryGbText => MemoryMb >= 1024
        ? $"{MemoryMb / 1024.0:F1} GB"
        : $"{MemoryMb} MB";

    public string UptimeText
    {
        get
        {
            if (_serverStartedAt is null || !IsRunning) return string.Empty;
            var uptime = DateTime.UtcNow - _serverStartedAt.Value;
            if (uptime.TotalDays >= 1)
                return $"{(int)uptime.TotalDays}d {uptime.Hours}h {uptime.Minutes}m uptime";
            if (uptime.TotalHours >= 1)
                return $"{(int)uptime.TotalHours}h {uptime.Minutes}m uptime";
            return $"{(int)uptime.TotalMinutes}m uptime";
        }
    }

    public string VmIpText => ActiveProfile?.VmIpAddress is { Length: > 0 } ip ? ip : "—";
    public bool   HasVmIp   => ActiveProfile?.VmIpAddress is { Length: > 0 };

    [ObservableProperty] private bool _isAdvancedExpanded;

    [RelayCommand]
    private void ToggleAdvanced() => IsAdvancedExpanded = !IsAdvancedExpanded;

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
        IServiceScopeFactory       scopeFactory,
        IServerProcessService      processService,
        IActiveProfileService      activeProfileService,
        IDiscordWebhookService     discordService)
    {
        _controlService       = controlService;
        _scopeFactory         = scopeFactory;
        _activeProfileService = activeProfileService;
        _discordService       = discordService;

        // Subscribe to process exit so unexpected crashes surface immediately (#131)
        processService.ProcessExited += OnServerProcessExited;
        processService.ServerStarted += (_, _) =>
        {
            _serverStartedAt = DateTime.UtcNow;
            Application.Current.Dispatcher.InvokeAsync(() => OnPropertyChanged(nameof(UptimeText)));
        };

        // Refresh when the user switches profiles (#139)
        _activeProfileService.ProfileChanged += (_, profile) =>
        {
            ActiveProfile = profile;
            _ = RefreshStatusAsync();
        };

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _refreshTimer.Tick += async (_, _) => await RefreshStatusAsync();
        _refreshTimer.Start();
    }

    // Fires on a thread-pool thread — must marshal to Dispatcher before touching UI state.
    private void OnServerProcessExited(object? sender, ServerProcessExitEventArgs e)
    {
        if (e.WasExpected) return;   // intentional stop — handled by RefreshStatusAsync

        _serverStartedAt = null;
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
        ActiveProfile = _activeProfileService.Current;
        await RefreshStatusAsync();
    }

    private async Task RefreshStatusAsync()
    {
        if (ActiveProfile is null) return;

        try
        {
            Status    = await _controlService.GetStatusAsync(ActiveProfile);
            LastError = null;
            if (Status is BattlegroupRuntimeStatus.Offline or BattlegroupRuntimeStatus.Unknown or BattlegroupRuntimeStatus.Error)
                _serverStartedAt = null;
            OnPropertyChanged(nameof(UptimeText));
            OnPropertyChanged(nameof(VmIpText));
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

    // Update via 'battlegroup update' — request with confirmation overlay
    [RelayCommand(CanExecute = nameof(CanRequestUpdate))]
    private void RequestUpdate()
    {
        PendingAction = PendingDashboardAction.Update;
        OnPropertyChanged(nameof(ShowConfirmation));
        OnPropertyChanged(nameof(ConfirmationTitle));
        OnPropertyChanged(nameof(ConfirmationBody));
    }
    private bool CanRequestUpdate() => HasProfile && !IsTransitioning && !IsUpdating;

    // Enable experimental swap memory
    [RelayCommand(CanExecute = nameof(CanEnableSwap))]
    private async Task EnableExperimentalSwapAsync()
    {
        if (ActiveProfile is null) return;
        IsEnablingSwap = true;
        SwapResult     = string.Empty;
        try
        {
            var (success, error) = await _controlService.EnableExperimentalSwapAsync(ActiveProfile);
            SwapResult = success
                ? "Swap memory enabled. Start the battlegroup to apply."
                : $"Failed: {error}";
        }
        catch (Exception ex)
        {
            SwapResult = $"Error: {ex.Message}";
        }
        finally
        {
            IsEnablingSwap = false;
            OnPropertyChanged(nameof(SwapStatusMessage));
        }
    }
    private bool CanEnableSwap() => HasProfile && IsStopped && !IsTransitioning && !IsUpdating && !IsEnablingSwap;

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

        IsUpdating     = true;
        UpdateLog      = string.Empty;
        UpdateProgress = 0;
        LastError      = null;

        try
        {
            // 'battlegroup update' handles everything inside the VM (SteamCMD, pod restart).
            // It requires SSH to be connected, which means the VM must be running.
            // If the server pods are running, the update will stop and restart them automatically.
            AppendUpdateLog("Starting battlegroup update — SteamCMD will run inside the VM…");
            AppendUpdateLog("This may take several minutes. Do not close the application.");

            await _controlService.UpdateBattlegroupAsync(
                ActiveProfile,
                line => AppendUpdateLog(line));

            UpdateProgress = 100;
            AppendUpdateLog("Update complete.");
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

    // ── Discord announcement (#171) ───────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(CanSendAnnouncement))]
    private async Task SendAnnouncementAsync()
    {
        if (string.IsNullOrWhiteSpace(AnnouncementText)) return;

        IsAnnouncementBusy       = true;
        AnnouncementStatusMessage = string.Empty;

        try
        {
            var serverName = ActiveProfile?.Name ?? "Server";
            await _discordService.SendAnnouncementAsync(AnnouncementText.Trim(), serverName);
            AnnouncementText          = string.Empty;
            AnnouncementStatusMessage = "Announcement sent.";
        }
        catch (Exception ex)
        {
            AnnouncementStatusMessage = $"Failed: {ex.Message}";
        }
        finally
        {
            IsAnnouncementBusy = false;
        }
    }

    private bool CanSendAnnouncement() => !IsAnnouncementBusy && !string.IsNullOrWhiteSpace(AnnouncementText);
}
