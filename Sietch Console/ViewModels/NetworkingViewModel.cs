using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using SietchConsole.Core.Interfaces;
using SietchConsole.Core.Models;

namespace Sietch_Console.ViewModels;

public partial class NetworkingViewModel : ObservableObject
{
    private readonly INetworkingService    _networkingService;
    private readonly IConfigurationService _configService;
    private readonly IServiceScopeFactory  _scopeFactory;
    private readonly IActiveProfileService _activeProfileService;

    private BattlegroupProfile?            _profile;
    private IReadOnlyList<BattlegroupPort> _ports = [];
    private NetworkInfo _networkInfo = new(null, null, null, null);

    // ── State ─────────────────────────────────────────────────────────
    [ObservableProperty] private bool   _isLoading;
    [ObservableProperty] private string _statusMessage = string.Empty;

    // ── Network info (#84, #85) ───────────────────────────────────────
    [ObservableProperty] private string _hostIp      = "—";
    [ObservableProperty] private string _hostAdapter = "—";
    [ObservableProperty] private string _vmIp        = "—";
    [ObservableProperty] private string _vmAdapter   = "—";

    // ── Required ports (#83) ─────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<BattlegroupPort> _requiredPorts = [];

    // ── Firewall (#87, #88) ───────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFirewallIssues))]
    [NotifyPropertyChangedFor(nameof(AllFirewallRulesOk))]
    private ObservableCollection<FirewallRuleStatus> _firewallStatuses = [];

    public bool HasFirewallIssues   => FirewallStatuses.Any(s => !s.IsConfigured);
    public bool AllFirewallRulesOk  => FirewallStatuses.Count > 0 && !HasFirewallIssues;

    // ── Connectivity (#89) ────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConnectivityTested))]
    private ObservableCollection<ConnectivityResult> _connectivityResults = [];

    public bool ConnectivityTested => ConnectivityResults.Count > 0;

    // ── Connection summary (#90) ─────────────────────────────────────
    [ObservableProperty] private string _connectionSummary = string.Empty;

    public NetworkingViewModel(
        INetworkingService    networkingService,
        IConfigurationService configService,
        IServiceScopeFactory  scopeFactory,
        IActiveProfileService activeProfileService)
    {
        _networkingService   = networkingService;
        _configService       = configService;
        _scopeFactory        = scopeFactory;
        _activeProfileService = activeProfileService;

        _activeProfileService.ProfileChanged += (_, profile) =>
        {
            _profile = profile;
            _ = LoadAllAsync();
        };
    }

    public async Task InitializeAsync()
    {
        _profile = _activeProfileService.Current;
        await LoadAllAsync();
    }

    private async Task LoadAllAsync()
    {
        IsLoading     = true;
        StatusMessage = string.Empty;
        try
        {
            // Detect network addresses
            _networkInfo = await _networkingService.DetectNetworkInfoAsync();
            HostIp      = _networkInfo.HostIp          ?? "Not detected";
            HostAdapter = _networkInfo.HostAdapterName ?? "—";
            VmIp        = _networkInfo.VmIp            ?? "Not detected";
            VmAdapter   = _networkInfo.VmAdapterName   ?? "—";

            // #140 – Persist detected VM IP back to the profile so other views can read it
            if (_profile is not null && _networkInfo.VmIp is not null
                && _networkInfo.VmIp != _profile.VmIpAddress)
            {
                _profile.VmIpAddress = _networkInfo.VmIp;
                using var scope2 = _scopeFactory.CreateScope();
                var profileRepo  = scope2.ServiceProvider.GetRequiredService<IBattlegroupProfileRepository>();
                await profileRepo.UpdateAsync(_profile);
            }

            // Resolve game port from active config
            int gamePort = 7777;
            if (_profile is not null)
            {
                var config = await _configService.LoadAsync(_profile);
                if (config is not null) gamePort = config.GamePort;
            }

            // Build port list
            _ports       = _networkingService.GetRequiredPorts(gamePort);
            RequiredPorts = new ObservableCollection<BattlegroupPort>(_ports);

            // Check firewall rules
            var statuses     = await _networkingService.CheckFirewallRulesAsync(_ports);
            FirewallStatuses = new ObservableCollection<FirewallRuleStatus>(statuses);
            OnPropertyChanged(nameof(HasFirewallIssues));
            OnPropertyChanged(nameof(AllFirewallRulesOk));

            // Generate connection summary
            ConnectionSummary = _networkingService.GenerateConnectionSummary(
                _profile?.Name, _networkInfo, _ports);
        }
        finally { IsLoading = false; }
    }

    // #88 – Create firewall rules with UAC elevation
    [RelayCommand]
    private async Task CreateFirewallRulesAsync()
    {
        if (_ports.Count == 0) return;
        IsLoading     = true;
        StatusMessage = string.Empty;
        try
        {
            await _networkingService.CreateFirewallRulesAsync(_ports);
            await Task.Delay(1200);
            var statuses     = await _networkingService.CheckFirewallRulesAsync(_ports);
            FirewallStatuses = new ObservableCollection<FirewallRuleStatus>(statuses);
            OnPropertyChanged(nameof(HasFirewallIssues));
            OnPropertyChanged(nameof(AllFirewallRulesOk));
            StatusMessage = HasFirewallIssues
                ? "Some rules could not be verified. You may need to run Sietch Console as Administrator."
                : "All firewall rules are now configured.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Firewall setup failed: {ex.Message}";
        }
        finally { IsLoading = false; }
    }

    // #89 – Local port listening test
    [RelayCommand]
    private async Task TestConnectivityAsync()
    {
        if (_ports.Count == 0) return;
        IsLoading     = true;
        StatusMessage = string.Empty;
        try
        {
            var results          = await _networkingService.TestLocalConnectivityAsync(_ports);
            ConnectivityResults  = new ObservableCollection<ConnectivityResult>(results);
            OnPropertyChanged(nameof(ConnectivityTested));
            StatusMessage = "Local port scan complete.";
        }
        finally { IsLoading = false; }
    }

    // #90 – Copy summary to clipboard
    [RelayCommand]
    private void CopyConnectionSummary()
    {
        if (!string.IsNullOrEmpty(ConnectionSummary))
            Clipboard.SetText(ConnectionSummary);
        StatusMessage = "Connection summary copied to clipboard.";
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAllAsync();
}
