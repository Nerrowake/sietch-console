using System.Management;
using SietchConsole.Core.Models;

namespace Sietch_Console.Services.Diagnostics.Checks;

public static class FirewallCheck
{
    // Ports required by the Dune: Awakening dedicated server
    private static readonly int[] RequiredPorts = [7777, 7778, 27015, 27016];

    public static DiagnosticsResult Run()
    {
        var result = new DiagnosticsResult { CheckName = "Windows Firewall" };

        try
        {
            bool firewallServiceRunning = false;

            using (var searcher = new ManagementObjectSearcher(
                @"root\cimv2",
                "SELECT State FROM Win32_Service WHERE Name = 'MpsSvc'"))
            {
                foreach (ManagementObject obj in searcher.Get())
                {
                    firewallServiceRunning = obj["State"]?.ToString() == "Running";
                    break;
                }
            }

            var portList = string.Join(", ", RequiredPorts);
            result.TechnicalMessage = $"Windows Firewall service (MpsSvc) running: {firewallServiceRunning}. Required ports: {portList}";

            if (!firewallServiceRunning)
            {
                result.Severity = "Warning";
                result.Title = "Windows Firewall Not Running";
                result.FriendlyMessage = "The Windows Firewall service is not running. While this means ports are not actively blocked, it also means your system has reduced protection.";
                result.RecommendedAction = $"Enable Windows Firewall and manually allow inbound traffic on ports {portList} (UDP and TCP) for the battlegroup server.";
            }
            else
            {
                result.Severity = "Warning";
                result.Title = $"Firewall Active — Ports {portList} May Need Rules";
                result.FriendlyMessage = $"Windows Firewall is running. You will need inbound firewall rules for ports {portList} (UDP/TCP) to allow players to connect to your battlegroup.";
                result.RecommendedAction = "Sietch Console can create these firewall rules automatically during setup, or you can add them manually in Windows Defender Firewall.";
            }
        }
        catch (Exception ex)
        {
            result.Severity = "Failure";
            result.Title = "Firewall (detection failed)";
            result.TechnicalMessage = ex.Message;
            result.FriendlyMessage = "Could not determine Windows Firewall status.";
        }

        return result;
    }
}
