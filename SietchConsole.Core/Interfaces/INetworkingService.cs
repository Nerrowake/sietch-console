using SietchConsole.Core.Models;

namespace SietchConsole.Core.Interfaces;

public interface INetworkingService
{
    // #83
    IReadOnlyList<BattlegroupPort> GetRequiredPorts(int gamePort = 7777);

    // #84, #85
    Task<NetworkInfo> DetectNetworkInfoAsync();

    // #87
    Task<IReadOnlyList<FirewallRuleStatus>> CheckFirewallRulesAsync(IReadOnlyList<BattlegroupPort> ports);

    // #88
    Task CreateFirewallRulesAsync(IReadOnlyList<BattlegroupPort> ports);

    // #89
    Task<IReadOnlyList<ConnectivityResult>> TestLocalConnectivityAsync(IReadOnlyList<BattlegroupPort> ports);

    // #90
    string GenerateConnectionSummary(string? serverName, NetworkInfo info, IReadOnlyList<BattlegroupPort> ports);
}
