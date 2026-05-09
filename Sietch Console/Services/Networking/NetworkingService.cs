using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using SietchConsole.Core.Interfaces;
using SietchConsole.Core.Models;

namespace Sietch_Console.Services.Networking;

public class NetworkingService : INetworkingService
{
    // #83 – Static port list derived from game port
    public IReadOnlyList<BattlegroupPort> GetRequiredPorts(int gamePort = 7777) =>
    [
        new("Game Traffic",  gamePort,     "UDP", "Primary port players use to connect to the battlegroup."),
        new("Server Beacon", gamePort + 1, "UDP", "Used by Steam and the server browser to advertise the server."),
        new("Steam Query",   27015,        "UDP", "Allows the Steam server browser to discover and display the server."),
        new("Steam Relay",   27016,        "UDP", "Used by Steam networking for relay and lobby services."),
    ];

    // #84, #85 – Detect host LAN IP and Hyper-V VM IP
    public async Task<NetworkInfo> DetectNetworkInfoAsync()
    {
        return await Task.Run(() =>
        {
            string? hostIp = null, hostAdapter = null, vmIp = null, vmAdapter = null;

            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                bool isVirtual = ni.Name.StartsWith("vEthernet", StringComparison.OrdinalIgnoreCase)
                              || ni.Description.Contains("Hyper-V", StringComparison.OrdinalIgnoreCase)
                              || ni.Description.Contains("Virtual", StringComparison.OrdinalIgnoreCase);

                var v4 = ni.GetIPProperties().UnicastAddresses
                    .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork
                                      && !System.Net.IPAddress.IsLoopback(a.Address));
                if (v4 is null) continue;

                var ip = v4.Address.ToString();

                if (isVirtual)
                {
                    vmIp      ??= ip;
                    vmAdapter ??= ni.Name;
                }
                else
                {
                    hostIp      ??= ip;
                    hostAdapter ??= ni.Name;
                }
            }

            return new NetworkInfo(hostIp, hostAdapter, vmIp, vmAdapter);
        });
    }

    // #87 – Check Windows Firewall for required inbound rules
    public async Task<IReadOnlyList<FirewallRuleStatus>> CheckFirewallRulesAsync(
        IReadOnlyList<BattlegroupPort> ports)
    {
        var found = await Task.Run(() =>
        {
            var output = RunProcess("netsh", "advfirewall firewall show rule name=all dir=in");
            var result = new HashSet<(int port, string proto)>();

            string? lastProto = null;
            foreach (var line in output.Split('\n'))
            {
                var t = line.Trim();
                if (t.StartsWith("Protocol:", StringComparison.OrdinalIgnoreCase))
                    lastProto = t["Protocol:".Length..].Trim().ToUpperInvariant();
                else if (t.StartsWith("LocalPort:", StringComparison.OrdinalIgnoreCase))
                {
                    var portStr = t["LocalPort:".Length..].Trim();
                    if (int.TryParse(portStr, out var p) && lastProto is not null)
                        result.Add((p, lastProto));
                }
            }
            return result;
        });

        return ports
            .Select(p => new FirewallRuleStatus(p,
                InboundExists: found.Contains((p.Port, p.Protocol.ToUpperInvariant()))))
            .ToList();
    }

    // #88 – Create Windows Firewall inbound rules (elevated via UAC)
    public async Task CreateFirewallRulesAsync(IReadOnlyList<BattlegroupPort> ports)
    {
        var sb = new StringBuilder();
        foreach (var p in ports)
            sb.AppendLine($"netsh advfirewall firewall add rule " +
                          $"name=\"Sietch Console - {p.Name}\" " +
                          $"dir=in action=allow protocol={p.Protocol} localport={p.Port}");

        var tmpFile = Path.Combine(Path.GetTempPath(), "sietch_fw_rules.bat");
        await File.WriteAllTextAsync(tmpFile, sb.ToString());

        var psi = new ProcessStartInfo("cmd.exe", $"/c \"{tmpFile}\"")
        {
            UseShellExecute = true,
            Verb            = "runas",
        };

        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException("Could not start elevated process.");
        await proc.WaitForExitAsync();

        try { File.Delete(tmpFile); } catch { /* best effort */ }
    }

    // #89 – Test whether each port appears to have something listening locally
    public async Task<IReadOnlyList<ConnectivityResult>> TestLocalConnectivityAsync(
        IReadOnlyList<BattlegroupPort> ports)
    {
        var netstat = await Task.Run(() => RunProcess("netstat", "-ano"));

        return ports.Select(p =>
        {
            // Match ":<port> " — works for both UDP (*:*) and TCP (LISTENING) output lines
            bool listening = netstat.Contains($":{p.Port} ", StringComparison.Ordinal)
                          || netstat.Contains($":{p.Port}\t", StringComparison.Ordinal);
            return new ConnectivityResult(p, listening);
        }).ToList();
    }

    // #90 – Generate a copyable plain-text connection summary
    public string GenerateConnectionSummary(
        string? serverName, NetworkInfo info, IReadOnlyList<BattlegroupPort> ports)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== Dune: Awakening Battlegroup — Connection Info ===");
        sb.AppendLine();
        if (!string.IsNullOrWhiteSpace(serverName))
            sb.AppendLine($"Server Name : {serverName}");
        sb.AppendLine($"Host LAN IP : {info.HostIp ?? "Not detected"}");
        if (info.VmIp is not null)
            sb.AppendLine($"VM IP       : {info.VmIp}");
        sb.AppendLine();
        sb.AppendLine("Required Ports (forward these in your router for external access):");
        foreach (var p in ports)
            sb.AppendLine($"  {p.Port,5}  {p.Protocol,-4}  {p.Name}");
        sb.AppendLine();
        sb.AppendLine("To Join (Direct Connect):");
        var gamePort = ports.FirstOrDefault();
        var joinIp   = info.HostIp ?? "YOUR_IP";
        if (gamePort is not null)
            sb.AppendLine($"  {joinIp}:{gamePort.Port}");
        sb.AppendLine();
        sb.AppendLine("Note: Players outside your network need your PUBLIC IP address.");
        sb.AppendLine("      Find it at: https://whatismyip.com");
        return sb.ToString().TrimEnd();
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static string RunProcess(string exe, string args)
    {
        try
        {
            var psi = new ProcessStartInfo(exe, args)
            {
                UseShellExecute        = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                CreateNoWindow         = true,
            };
            using var proc = Process.Start(psi);
            if (proc is null) return string.Empty;
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit();
            return output;
        }
        catch { return string.Empty; }
    }
}
