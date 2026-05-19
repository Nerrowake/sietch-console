using System.Management;
using SietchConsole.Core.Models;

namespace Sietch_Console.Services.Diagnostics.Checks;

public static class MemoryCheck
{
    private const long MinimumBytes     = 20L * 1024 * 1024 * 1024; // 20 GB — Funcom minimum
    private const long RecommendedBytes = 32L * 1024 * 1024 * 1024; // 32 GB

    public static DiagnosticsResult Run()
    {
        var result = new DiagnosticsResult { CheckName = "System Memory" };

        try
        {
            long totalBytes = 0;

            using (var searcher = new ManagementObjectSearcher(
                @"root\cimv2",
                "SELECT TotalPhysicalMemory FROM Win32_ComputerSystem"))
            {
                foreach (ManagementObject obj in searcher.Get())
                {
                    totalBytes = Convert.ToInt64(obj["TotalPhysicalMemory"]);
                    break;
                }
            }

            double totalGb = totalBytes / (1024.0 * 1024 * 1024);
            result.TechnicalMessage = $"Total physical memory: {totalGb:F1} GB ({totalBytes:N0} bytes)";
            result.Title = $"{totalGb:F0} GB RAM Installed";

            if (totalBytes >= RecommendedBytes)
            {
                result.Severity = "Pass";
                result.FriendlyMessage = $"You have {totalGb:F0} GB of RAM, which comfortably meets the requirements for hosting a battlegroup.";
            }
            else if (totalBytes >= MinimumBytes)
            {
                result.Severity = "Warning";
                result.FriendlyMessage = $"You have {totalGb:F0} GB of RAM. The 20 GB minimum is met, but 32 GB or more is recommended when running multiple game servers.";
                result.RecommendedAction = "Consider upgrading to 32 GB or more RAM for the best multi-server hosting experience. Enable Swap Memory in the battlegroup settings to reduce per-server requirements.";
            }
            else
            {
                result.Severity = "Failure";
                result.FriendlyMessage = $"You only have {totalGb:F0} GB of RAM. Funcom requires at least 20 GB to host a Dune: Awakening battlegroup.";
                result.RecommendedAction = "Upgrade your system RAM to at least 20 GB. Enabling Swap Memory in the battlegroup settings can help reduce per-server memory requirements.";
            }
        }
        catch (Exception ex)
        {
            result.Severity = "Failure";
            result.Title = "System Memory (detection failed)";
            result.TechnicalMessage = ex.Message;
            result.FriendlyMessage = "Could not determine the amount of installed RAM.";
        }

        return result;
    }
}
