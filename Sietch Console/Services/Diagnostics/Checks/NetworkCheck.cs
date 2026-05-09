using System.Net.NetworkInformation;
using SietchConsole.Core.Models;

namespace Sietch_Console.Services.Diagnostics.Checks;

public static class NetworkCheck
{
    public static DiagnosticsResult Run()
    {
        var result = new DiagnosticsResult { CheckName = "Network Adapter" };

        try
        {
            var adapters = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up
                         && n.NetworkInterfaceType != NetworkInterfaceType.Loopback
                         && n.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                .ToList();

            bool hasConnectivity = NetworkInterface.GetIsNetworkAvailable();

            // Look for a physical Ethernet adapter suitable for a Hyper-V external switch
            var ethernetAdapters = adapters
                .Where(n => n.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
                .ToList();

            var adapterNames = string.Join(", ", adapters.Select(a => a.Name));
            result.TechnicalMessage = $"Active adapters: {adapters.Count} ({adapterNames}). Network available: {hasConnectivity}";

            if (!hasConnectivity || adapters.Count == 0)
            {
                result.Severity = "Failure";
                result.Title = "No Network Connectivity";
                result.FriendlyMessage = "No active network connection was detected. An active network is required to host a battlegroup.";
                result.RecommendedAction = "Connect your PC to a network via Ethernet or Wi-Fi and try again.";
            }
            else if (ethernetAdapters.Count == 0)
            {
                result.Severity = "Warning";
                result.Title = $"Connected via Wi-Fi ({adapters.First().Name})";
                result.FriendlyMessage = "You are connected via Wi-Fi. Hyper-V external virtual switches work best with a wired Ethernet connection for reliable battlegroup hosting.";
                result.RecommendedAction = "Consider connecting via Ethernet for a more stable hosting experience. Wi-Fi may work but is not recommended.";
            }
            else
            {
                result.Severity = "Pass";
                result.Title = $"Ethernet Connected ({ethernetAdapters.First().Name})";
                result.FriendlyMessage = $"An active Ethernet adapter was found and is suitable for a Hyper-V external virtual switch.";
            }
        }
        catch (Exception ex)
        {
            result.Severity = "Failure";
            result.Title = "Network Adapter (detection failed)";
            result.TechnicalMessage = ex.Message;
            result.FriendlyMessage = "Could not determine network adapter status.";
        }

        return result;
    }
}
