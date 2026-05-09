using SietchConsole.Core.Interfaces;
using System.IO;

namespace Sietch_Console.Services.Installation;

public class ServerPackageService : IServerPackageService
{
    // Known folder names for the Dune: Awakening dedicated server in Steam libraries
    private static readonly string[] KnownServerFolderNames =
    [
        "DuneAwakeningDedicatedServer",
        "Dune Awakening Dedicated Server",
        "DuneAwakeningSandboxServer",
    ];

    // Files that indicate a valid server installation
    private static readonly string[] ServerMarkerFiles =
    [
        "initial-setup.bat",
        "battlegroup.bat",
        "DedicatedServer.exe",
        "DuneServer.exe",
    ];

    public bool IsServerPackagePresent(string installPath)
    {
        if (!Directory.Exists(installPath)) return false;
        return ServerMarkerFiles.Any(f =>
            File.Exists(Path.Combine(installPath, f)) ||
            FindInSubdirectories(installPath, f, 3) is not null);
    }

    public string? FindSetupScript(string installPath)
        => FindInSubdirectories(installPath, "initial-setup.bat", 4);

    public string? FindBattlegroupScript(string installPath)
        => FindInSubdirectories(installPath, "battlegroup.bat", 4);

    public string? LocateInSteamLibraries(IReadOnlyList<string> libraryFolders)
    {
        foreach (var lib in libraryFolders)
        {
            var commonPath = Path.Combine(lib, "common");
            if (!Directory.Exists(commonPath)) continue;

            foreach (var folderName in KnownServerFolderNames)
            {
                var candidate = Path.Combine(commonPath, folderName);
                if (Directory.Exists(candidate) && IsServerPackagePresent(candidate))
                    return candidate;
            }

            // Also scan all folders for marker files if known names don't match
            foreach (var dir in Directory.GetDirectories(commonPath))
            {
                if (IsServerPackagePresent(dir))
                    return dir;
            }
        }
        return null;
    }

    private static string? FindInSubdirectories(string root, string fileName, int maxDepth)
    {
        if (maxDepth < 0) return null;

        var direct = Path.Combine(root, fileName);
        if (File.Exists(direct)) return direct;

        try
        {
            foreach (var dir in Directory.GetDirectories(root))
            {
                var result = FindInSubdirectories(dir, fileName, maxDepth - 1);
                if (result is not null) return result;
            }
        }
        catch (UnauthorizedAccessException) { }

        return null;
    }
}
