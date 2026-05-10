using SietchConsole.Core.Interfaces;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;

namespace Sietch_Console.Services.Installation;

public class SteamCmdService : ISteamCmdService
{
    private const string DownloadUrl = "https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip";

    private static readonly string ManagedDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SietchConsole", "steamcmd");

    // Search order: our managed copy first, then common manual installs
    private static readonly string[] SearchPaths =
    [
        Path.Combine(ManagedDir, "steamcmd.exe"),
        @"C:\steamcmd\steamcmd.exe",
        @"C:\Program Files (x86)\Steam\steamcmd.exe",
    ];

    public string? FindSteamCmd()
        => SearchPaths.FirstOrDefault(File.Exists);

    public async Task<string> EnsureSteamCmdAsync(IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var existing = FindSteamCmd();
        if (existing is not null) return existing;

        Directory.CreateDirectory(ManagedDir);

        var zipPath = Path.Combine(ManagedDir, "steamcmd.zip");
        var exePath = Path.Combine(ManagedDir, "steamcmd.exe");

        progress?.Report("Downloading SteamCMD…");
        using (var http = new HttpClient())
        using (var response = await http.GetAsync(DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct))
        {
            response.EnsureSuccessStatusCode();
            await using var fs = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await response.Content.CopyToAsync(fs, ct);
        }

        progress?.Report("Extracting SteamCMD…");
        ZipFile.ExtractToDirectory(zipPath, ManagedDir, overwriteFiles: true);
        File.Delete(zipPath);

        // First run completes SteamCMD's own self-update before any real command
        progress?.Report("Initializing SteamCMD…");
        await RunCommandAsync(exePath, "+quit", null, ct);

        return exePath;
    }

    public async Task<int> RunCommandAsync(
        string steamCmdPath,
        string arguments,
        Action<string>? onOutput = null,
        CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName               = steamCmdPath,
            Arguments              = arguments,
            WorkingDirectory       = Path.GetDirectoryName(steamCmdPath)!,
            UseShellExecute        = false,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            CreateNoWindow         = true,
        };

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        process.OutputDataReceived += (_, e) => { if (e.Data is not null) onOutput?.Invoke(e.Data); };
        process.ErrorDataReceived  += (_, e) => { if (e.Data is not null) onOutput?.Invoke($"[err] {e.Data}"); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(ct);
        return process.ExitCode;
    }
}
