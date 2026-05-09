using Microsoft.Win32;
using SietchConsole.Core.Models;

namespace Sietch_Console.Services.Diagnostics.Checks;

public static class WindowsVersionCheck
{
    public static DiagnosticsResult Run()
    {
        var result = new DiagnosticsResult { CheckName = "Windows Version" };

        try
        {
            var version = Environment.OSVersion.Version;
            var build = version.Build;

            var editionId = Registry.GetValue(
                @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion",
                "EditionID", null) as string ?? "Unknown";

            var displayVersion = Registry.GetValue(
                @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion",
                "DisplayVersion", null) as string ?? build.ToString();

            bool isWin10OrLater = version.Major >= 10;
            bool isWin11 = build >= 22000;
            string winLabel = isWin11 ? $"Windows 11 {displayVersion}" : $"Windows 10 {displayVersion}";

            bool supportsHyperV = editionId is "Professional" or "Enterprise" or "Education"
                or "ProfessionalEducation" or "ProfessionalWorkstation" or "ServerStandard"
                or "ServerDatacenter";

            result.Title = $"{winLabel} ({editionId})";
            result.TechnicalMessage = $"Build {build}, Edition: {editionId}";

            if (!isWin10OrLater)
            {
                result.Severity = "Failure";
                result.FriendlyMessage = "Your Windows version is too old to run Dune: Awakening battlegroups.";
                result.RecommendedAction = "Upgrade to Windows 10 Pro or Windows 11 Pro.";
            }
            else if (!supportsHyperV)
            {
                result.Severity = "Failure";
                result.FriendlyMessage = $"Windows {editionId} does not support Hyper-V, which is required for hosting a battlegroup.";
                result.RecommendedAction = "Upgrade your Windows edition to Pro, Enterprise, or Education to enable Hyper-V.";
            }
            else
            {
                result.Severity = "Pass";
                result.FriendlyMessage = $"Your Windows edition supports Hyper-V and meets the OS requirement.";
            }
        }
        catch (Exception ex)
        {
            result.Severity = "Failure";
            result.Title = "Windows Version (detection failed)";
            result.TechnicalMessage = ex.Message;
            result.FriendlyMessage = "Could not determine your Windows version.";
        }

        return result;
    }
}
