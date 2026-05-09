using System.Management;
using SietchConsole.Core.Models;

namespace Sietch_Console.Services.Diagnostics.Checks;

public static class MemoryCheck
{
    private const long MinimumBytes = 8L * 1024 * 1024 * 1024;     // 8 GB
    private const long RecommendedBytes = 16L * 1024 * 1024 * 1024; // 16 GB

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
                result.FriendlyMessage = $"You have {totalGb:F0} GB of RAM, which meets the recommended requirement for hosting a battlegroup.";
            }
            else if (totalBytes >= MinimumBytes)
            {
                result.Severity = "Warning";
                result.FriendlyMessage = $"You have {totalGb:F0} GB of RAM. The minimum requirement is met, but 16 GB or more is recommended for stable battlegroup hosting.";
                result.RecommendedAction = "Consider upgrading to 16 GB or more RAM for the best hosting experience.";
            }
            else
            {
                result.Severity = "Failure";
                result.FriendlyMessage = $"You only have {totalGb:F0} GB of RAM. At least 8 GB is required to host a Dune: Awakening battlegroup.";
                result.RecommendedAction = "Upgrade your system RAM to at least 8 GB (16 GB recommended) before attempting to host a battlegroup.";
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
