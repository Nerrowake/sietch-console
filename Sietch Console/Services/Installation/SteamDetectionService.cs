using Microsoft.Win32;
using SietchConsole.Core.Interfaces;
using System.IO;

namespace Sietch_Console.Services.Installation;

public class SteamDetectionService : ISteamDetectionService
{
    private static readonly string[] CommonSteamPaths =
    [
        @"C:\Program Files (x86)\Steam",
        @"C:\Program Files\Steam",
    ];

    public string? DetectSteamPath()
    {
        // Try registry first (most reliable)
        var regPath = Registry.GetValue(
            @"HKEY_CURRENT_USER\SOFTWARE\Valve\Steam", "SteamPath", null) as string;
        if (!string.IsNullOrEmpty(regPath) && Directory.Exists(regPath))
            return regPath;

        regPath = Registry.GetValue(
            @"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Valve\Steam", "InstallPath", null) as string;
        if (!string.IsNullOrEmpty(regPath) && Directory.Exists(regPath))
            return regPath;

        // Fall back to common install locations
        return CommonSteamPaths.FirstOrDefault(Directory.Exists);
    }

    public bool IsSteamInstalled() => DetectSteamPath() is not null;

    public IReadOnlyList<string> GetLibraryFolders(string steamPath)
    {
        var folders = new List<string> { Path.Combine(steamPath, "steamapps") };

        // Parse libraryfolders.vdf for additional Steam library locations
        var vdfPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(vdfPath)) return folders;

        foreach (var line in File.ReadLines(vdfPath))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith('"')) continue;

            var parts = trimmed.Split('"', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && parts[0] == "path")
            {
                var libPath = Path.Combine(parts[1].Replace(@"\\", @"\"), "steamapps");
                if (Directory.Exists(libPath) && !folders.Contains(libPath))
                    folders.Add(libPath);
            }
        }

        return folders;
    }
}
