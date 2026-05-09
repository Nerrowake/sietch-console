using System.IO;
using SietchConsole.Core.Models;

namespace Sietch_Console.Services.Diagnostics.Checks;

public static class DiskSpaceCheck
{
    private const long MinimumFreeBytes = 50L * 1024 * 1024 * 1024;     // 50 GB
    private const long RecommendedFreeBytes = 100L * 1024 * 1024 * 1024; // 100 GB

    public static DiagnosticsResult Run()
    {
        var result = new DiagnosticsResult { CheckName = "Disk Space" };

        try
        {
            var systemRoot = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.System))
                             ?? "C:\\";
            var drive = new DriveInfo(systemRoot);

            long freeBytes = drive.AvailableFreeSpace;
            long totalBytes = drive.TotalSize;
            double freeGb = freeBytes / (1024.0 * 1024 * 1024);
            double totalGb = totalBytes / (1024.0 * 1024 * 1024);

            result.TechnicalMessage = $"Drive {drive.Name}: {freeGb:F1} GB free of {totalGb:F0} GB total";
            result.Title = $"{freeGb:F0} GB Free on {drive.Name.TrimEnd('\\')}";

            if (freeBytes >= RecommendedFreeBytes)
            {
                result.Severity = "Pass";
                result.FriendlyMessage = $"You have {freeGb:F0} GB free on your system drive, which is sufficient for server files, updates, logs, and backups.";
            }
            else if (freeBytes >= MinimumFreeBytes)
            {
                result.Severity = "Warning";
                result.FriendlyMessage = $"You have {freeGb:F0} GB free. The minimum is met, but 100 GB or more is recommended to comfortably accommodate server files, updates, and backups.";
                result.RecommendedAction = "Free up additional disk space or consider installing to a drive with more available space.";
            }
            else
            {
                result.Severity = "Failure";
                result.FriendlyMessage = $"You only have {freeGb:F0} GB free on your system drive. At least 50 GB of free space is required.";
                result.RecommendedAction = "Free up disk space before proceeding. Delete unused files, uninstall unused programs, or add a larger drive.";
            }
        }
        catch (Exception ex)
        {
            result.Severity = "Failure";
            result.Title = "Disk Space (detection failed)";
            result.TechnicalMessage = ex.Message;
            result.FriendlyMessage = "Could not determine available disk space.";
        }

        return result;
    }
}
