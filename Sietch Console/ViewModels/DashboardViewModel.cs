using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using SietchConsole.Core.Interfaces;
using SietchConsole.Core.Models;
using System.Windows.Threading;

namespace Sietch_Console.ViewModels;

public enum PendingDashboardAction { None, Stop, Restart }

public partial class DashboardViewModel : ObservableObject
{
    private readonly IBattlegroupControlService _controlService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DispatcherTimer _refreshTimer;

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
    private BattlegroupRuntimeStatus _status = BattlegroupRuntimeStatus.Unknown;

    [ObservableProperty] private bool _isTransitioning;
    [ObservableProperty] private PendingDashboardAction _pendingAction = PendingDashboardAction.None;

    public bool HasProfile => ActiveProfile is not null;
    public string ProfileName => ActiveProfile?.Name ?? "No battlegroup configured";
    public string VmName => ActiveProfile?.VmName ?? "—";
    public bool IsRunning => Status == BattlegroupRuntimeStatus.Running;
    public bool IsStopped => Status is BattlegroupRuntimeStatus.Offline or BattlegroupRuntimeStatus.Unknown;
    public bool ShowConfirmation => PendingAction != PendingDashboardAction.None;

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

    public string ConfirmationTitle => PendingAction == PendingDashboardAction.Stop
        ? "Stop Battlegroup?" : "Restart Battlegroup?";

    public string ConfirmationBody => PendingAction == PendingDashboardAction.Stop
        ? "This will shut down the server and disconnect all active players."
        : "This will restart the server and briefly disconnect all active players.";

    public DashboardViewModel(
        IBattlegroupControlService controlService,
        IServiceScopeFactory scopeFactory)
    {
        _controlService = controlService;
        _scopeFactory = scopeFactory;

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _refreshTimer.Tick += async (_, _) => await RefreshStatusAsync();
        _refreshTimer.Start();
    }

    public async Task InitializeAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var settingsRepo = scope.ServiceProvider.GetRequiredService<IApplicationSettingsRepository>();
        var profileRepo = scope.ServiceProvider.GetRequiredService<IBattlegroupProfileRepository>();

        var settings = await settingsRepo.GetAsync();
        if (int.TryParse(settings.LastOpenedBattlegroupId, out var id))
            ActiveProfile = await profileRepo.GetByIdAsync(id);
        else
            ActiveProfile = (await profileRepo.GetAllAsync()).FirstOrDefault();

        await RefreshStatusAsync();
    }

    private async Task RefreshStatusAsync()
    {
        if (ActiveProfile is null) return;
        Status = await _controlService.GetStatusAsync(ActiveProfile);
    }

    // #51 – Start
    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync()
    {
        if (ActiveProfile is null) return;
        IsTransitioning = true;
        Status = BattlegroupRuntimeStatus.Starting;
        try { await _controlService.StartAsync(ActiveProfile); }
        finally { IsTransitioning = false; await RefreshStatusAsync(); }
    }
    private bool CanStart() => HasProfile && IsStopped && !IsTransitioning;

    // #52 – Stop (with confirmation)
    [RelayCommand(CanExecute = nameof(CanStop))]
    private void RequestStop()
    {
        PendingAction = PendingDashboardAction.Stop;
        OnPropertyChanged(nameof(ShowConfirmation));
        OnPropertyChanged(nameof(ConfirmationTitle));
        OnPropertyChanged(nameof(ConfirmationBody));
    }
    private bool CanStop() => HasProfile && IsRunning && !IsTransitioning;

    // #53 – Restart (with confirmation)
    [RelayCommand(CanExecute = nameof(CanStop))]
    private void RequestRestart()
    {
        PendingAction = PendingDashboardAction.Restart;
        OnPropertyChanged(nameof(ShowConfirmation));
        OnPropertyChanged(nameof(ConfirmationTitle));
        OnPropertyChanged(nameof(ConfirmationBody));
    }

    // #57 – Confirm / Cancel
    [RelayCommand]
    private async Task ConfirmActionAsync()
    {
        var action = PendingAction;
        PendingAction = PendingDashboardAction.None;
        OnPropertyChanged(nameof(ShowConfirmation));

        if (ActiveProfile is null) return;
        IsTransitioning = true;
        Status = action == PendingDashboardAction.Stop
            ? BattlegroupRuntimeStatus.Stopping
            : BattlegroupRuntimeStatus.Stopping;
        try
        {
            if (action == PendingDashboardAction.Stop)
                await _controlService.StopAsync(ActiveProfile);
            else
                await _controlService.RestartAsync(ActiveProfile);
        }
        finally { IsTransitioning = false; await RefreshStatusAsync(); }
    }

    [RelayCommand]
    private void CancelAction()
    {
        PendingAction = PendingDashboardAction.None;
        OnPropertyChanged(nameof(ShowConfirmation));
    }

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
}
