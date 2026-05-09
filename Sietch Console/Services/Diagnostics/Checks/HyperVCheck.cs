using System.Management;
using SietchConsole.Core.Models;

namespace Sietch_Console.Services.Diagnostics.Checks;

public static class HyperVCheck
{
    public static DiagnosticsResult Run()
    {
        var result = new DiagnosticsResult { CheckName = "Hyper-V" };

        try
        {
            bool featureEnabled = false;
            bool serviceRunning = false;

            // Check Hyper-V optional feature state via WMI
            using (var searcher = new ManagementObjectSearcher(
                @"root\cimv2",
                "SELECT * FROM Win32_OptionalFeature WHERE Name = 'Microsoft-Hyper-V'"))
            {
                foreach (ManagementObject obj in searcher.Get())
                {
                    // InstallState: 1 = Enabled, 2 = Disabled, 3 = Absent
                    var state = Convert.ToUInt32(obj["InstallState"]);
                    featureEnabled = state == 1;
                    break;
                }
            }

            // Cross-check: VMMS (Virtual Machine Management Service) running
            using (var searcher = new ManagementObjectSearcher(
                @"root\cimv2",
                "SELECT State FROM Win32_Service WHERE Name = 'vmms'"))
            {
                foreach (ManagementObject obj in searcher.Get())
                {
                    serviceRunning = obj["State"]?.ToString() == "Running";
                    break;
                }
            }

            result.TechnicalMessage = $"Feature enabled: {featureEnabled}, VMMS service running: {serviceRunning}";

            if (featureEnabled && serviceRunning)
            {
                result.Severity = "Pass";
                result.Title = "Hyper-V Enabled and Running";
                result.FriendlyMessage = "Hyper-V is installed and the Virtual Machine Management Service is active.";
            }
            else if (featureEnabled)
            {
                result.Severity = "Warning";
                result.Title = "Hyper-V Installed but Service Not Running";
                result.FriendlyMessage = "Hyper-V is installed but its management service is not currently running.";
                result.RecommendedAction = "Open Services and start the 'Hyper-V Virtual Machine Management' service, or restart your PC.";
            }
            else
            {
                result.Severity = "Failure";
                result.Title = "Hyper-V Not Enabled";
                result.FriendlyMessage = "Hyper-V is not enabled on this system. It is required to host a Dune: Awakening battlegroup.";
                result.RecommendedAction = "Enable Hyper-V via Windows Features (Turn Windows features on or off) or run: Enable-WindowsOptionalFeature -Online -FeatureName Microsoft-Hyper-V -All";
            }
        }
        catch (Exception ex)
        {
            result.Severity = "Failure";
            result.Title = "Hyper-V (detection failed)";
            result.TechnicalMessage = ex.Message;
            result.FriendlyMessage = "Could not determine Hyper-V status.";
        }

        return result;
    }
}
