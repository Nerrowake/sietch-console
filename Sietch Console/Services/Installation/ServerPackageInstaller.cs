using SietchConsole.Core.Interfaces;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Sietch_Console.Services.Installation;

public class ServerPackageInstaller : IServerPackageInstaller
{
    // TODO: Verify this App ID against Funcom's Steam dedicated-server page before shipping.
    private const int DuneServerAppId = 2369390;

    private static readonly string[] RequiredMarkers =
    [
        "DedicatedServer.exe",
        "DuneServer.exe",
        "initial-setup.bat",
    ];

    private readonly ISteamCmdService _steamCmd;

    public ServerPackageInstaller(ISteamCmdService steamCmd) => _steamCmd = steamCmd;

    public async Task InstallAsync(
        string installPath,
        IProgress<double> progress,
        Action<string> onOutput,
        CancellationToken ct = default)
        => await RunSteamCmdInstallAsync(installPath, progress, onOutput, ct);

    public async Task UpdateAsync(
        string installPath,
        IProgress<double> progress,
        Action<string> onOutput,
        CancellationToken ct = default)
        => await RunSteamCmdInstallAsync(installPath, progress, onOutput, ct);

    public async Task<bool?> CheckForUpdateAsync(string installPath, CancellationToken ct = default)
    {
        var installedId = GetInstalledBuildId(installPath);
        if (installedId is null) return null;

        var steamCmdPath = _steamCmd.FindSteamCmd();
        if (steamCmdPath is null) return null;

        var output = new StringBuilder();
        var args = $"+login anonymous +app_info_update 1 +app_info_print {DuneServerAppId} +quit";

        await _steamCmd.RunCommandAsync(steamCmdPath, args, line => output.AppendLine(line), ct);

        var latestId = ParseBuildIdFromAppInfo(output.ToString());
        if (latestId is null) return null;

        return latestId != installedId;
    }

    public string? GetInstalledBuildId(string installPath)
    {
        var acfPath = Path.Combine(installPath, "steamapps", $"appmanifest_{DuneServerAppId}.acf");
        if (!File.Exists(acfPath)) return null;

        foreach (var line in File.ReadLines(acfPath))
        {
            var match = Regex.Match(line, @"""buildid""\s+""(\d+)""");
            if (match.Success) return match.Groups[1].Value;
        }

        return null;
    }

    public bool VerifyInstallation(string installPath)
        => RequiredMarkers.Any(m =>
            File.Exists(Path.Combine(installPath, m)) ||
            FindInSubdirectories(installPath, m, 3) is not null);

    private async Task RunSteamCmdInstallAsync(
        string installPath,
        IProgress<double> progress,
        Action<string> onOutput,
        CancellationToken ct)
    {
        Directory.CreateDirectory(installPath);

        progress.Report(5);
        onOutput("Ensuring SteamCMD is available…");
        var steamCmdPath = await _steamCmd.EnsureSteamCmdAsync(
            new Progress<string>(msg => onOutput(msg)), ct);

        progress.Report(15);
        onOutput($"Starting server file download via SteamCMD…");
        onOutput($"Install path: {installPath}");

        int linesRead = 0;
        var args = $"+login anonymous +force_install_dir \"{installPath}\" +app_update {DuneServerAppId} validate +quit";

        var exitCode = await _steamCmd.RunCommandAsync(steamCmdPath, args, line =>
        {
            onOutput(line);
            linesRead++;
            progress.Report(Math.Min(95.0, 15.0 + linesRead * 0.5));
        }, ct);

        if (exitCode != 0)
            throw new InvalidOperationException(
                $"SteamCMD exited with code {exitCode}. Check the log above for details.");

        progress.Report(100);
        onOutput("Server files installed/updated successfully.");
    }

    // Parses the build ID out of the verbose output from +app_info_print
    private static string? ParseBuildIdFromAppInfo(string output)
    {
        var match = Regex.Match(output, @"""buildid""\s+""(\d+)""");
        return match.Success ? match.Groups[1].Value : null;
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
