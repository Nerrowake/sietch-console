using System.Management;
using SietchConsole.Core.Models;

namespace Sietch_Console.Services.Diagnostics.Checks;

public static class VirtualizationCheck
{
    public static DiagnosticsResult Run()
    {
        var result = new DiagnosticsResult { CheckName = "Hardware Virtualization" };

        try
        {
            bool firmwareEnabled = false;
            bool vmMonitorModeExtensions = false;

            using (var searcher = new ManagementObjectSearcher(
                @"root\cimv2",
                "SELECT VirtualizationFirmwareEnabled, VMMonitorModeExtensions FROM Win32_Processor"))
            {
                foreach (ManagementObject obj in searcher.Get())
                {
                    firmwareEnabled = Convert.ToBoolean(obj["VirtualizationFirmwareEnabled"]);
                    vmMonitorModeExtensions = Convert.ToBoolean(obj["VMMonitorModeExtensions"]);
                    break;
                }
            }

            result.TechnicalMessage = $"VirtualizationFirmwareEnabled: {firmwareEnabled}, VMMonitorModeExtensions: {vmMonitorModeExtensions}";

            if (firmwareEnabled && vmMonitorModeExtensions)
            {
                result.Severity = "Pass";
                result.Title = "Hardware Virtualization Enabled";
                result.FriendlyMessage = "CPU virtualization is enabled in your BIOS/UEFI and ready for Hyper-V.";
            }
            else if (!firmwareEnabled)
            {
                result.Severity = "Failure";
                result.Title = "Virtualization Disabled in BIOS";
                result.FriendlyMessage = "Hardware virtualization (VT-x/AMD-V) is not enabled in your BIOS/UEFI settings. Hyper-V requires this.";
                result.RecommendedAction = "Restart your PC and enter BIOS/UEFI settings. Enable 'Intel Virtualization Technology' (VT-x) or 'AMD-V' / 'SVM Mode', then save and reboot.";
            }
            else
            {
                result.Severity = "Warning";
                result.Title = "Virtualization Partially Available";
                result.FriendlyMessage = "CPU virtualization support was detected but may not be fully enabled.";
                result.RecommendedAction = "Check your BIOS settings to ensure all virtualization options are enabled.";
            }
        }
        catch (Exception ex)
        {
            result.Severity = "Failure";
            result.Title = "Hardware Virtualization (detection failed)";
            result.TechnicalMessage = ex.Message;
            result.FriendlyMessage = "Could not query CPU virtualization status.";
        }

        return result;
    }
}
