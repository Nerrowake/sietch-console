using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Windows;
using SietchConsole.Core.Interfaces;
using SietchConsole.Core.Models;

namespace Sietch_Console.Services.Update;

/// <summary>
/// Queries the GitHub Releases API for the latest Sietch Console release
/// and manages the download + installer launch flow (#145–#148).
/// </summary>
public class AppUpdateService : IAppUpdateService, IDisposable
{
    private const string ReleasesApiUrl =
        "https://api.github.com/repos/michaelstoffer/sietch-console/releases/latest";

    private const string InstallerAssetSuffix = "Setup.exe";

    private readonly HttpClient _http;

    public AppUpdateService()
    {
        _http = new HttpClient();
        _http.DefaultRequestHeaders.Add("User-Agent", "SietchConsole-AutoUpdate/1.0");
        _http.DefaultRequestHeaders.Add("Accept",     "application/vnd.github+json");
        _http.Timeout = TimeSpan.FromSeconds(30);
    }

    // ── #145 – Check for update ───────────────────────────────────────────────

    public async Task<AppUpdateInfo?> CheckForUpdateAsync(CancellationToken ct = default)
    {
        try
        {
            var release = await _http.GetFromJsonAsync<GitHubRelease>(ReleasesApiUrl, ct);
            if (release is null) return null;

            if (!IsNewerVersion(release.TagName)) return null;

            var installerAsset = release.Assets
                .FirstOrDefault(a => a.Name.EndsWith(InstallerAssetSuffix,
                                                     StringComparison.OrdinalIgnoreCase));

            return new AppUpdateInfo(
                TagName              : release.TagName,
                ReleaseName          : release.Name,
                HtmlUrl              : release.HtmlUrl,
                InstallerDownloadUrl : installerAsset?.BrowserDownloadUrl,
                ReleaseNotes         : release.Body?.Length > 800
                                           ? release.Body[..800] + "…"
                                           : release.Body);
        }
        catch
        {
            // Network failure, rate limit, etc. — silently swallow in alpha.
            return null;
        }
    }

    // ── #148 – Silent background download ────────────────────────────────────

    public async Task<string> DownloadInstallerAsync(
        AppUpdateInfo      info,
        IProgress<double>? progress = null,
        CancellationToken  ct       = default)
    {
        if (string.IsNullOrWhiteSpace(info.InstallerDownloadUrl))
            throw new InvalidOperationException("No installer download URL available for this release.");

        var tempDir  = Path.Combine(Path.GetTempPath(), "SietchConsole_Update");
        Directory.CreateDirectory(tempDir);
        var fileName = Path.GetFileName(info.InstallerDownloadUrl);
        var dest     = Path.Combine(tempDir, fileName);

        using var response = await _http.GetAsync(
            info.InstallerDownloadUrl,
            HttpCompletionOption.ResponseHeadersRead, ct);

        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? -1L;

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        await using var file   = File.Create(dest);

        var buffer    = new byte[81920];
        long received = 0;
        int  read;

        while ((read = await stream.ReadAsync(buffer, ct)) > 0)
        {
            await file.WriteAsync(buffer.AsMemory(0, read), ct);
            received += read;
            if (total > 0)
                progress?.Report((double)received / total);
        }

        progress?.Report(1.0);
        return dest;
    }

    // ── #147 – Launch installer and exit ─────────────────────────────────────

    public void LaunchInstallerAndExit(string installerPath)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName        = installerPath,
            UseShellExecute = true,
        });

        Application.Current.Dispatcher.InvokeAsync(() => Application.Current.Shutdown());
    }

    public void Dispose() => _http.Dispose();

    // ── Version comparison ────────────────────────────────────────────────────

    private static bool IsNewerVersion(string tagName)
    {
        // tagName is like "v0.3.0-alpha.1"; current version is from InformationalVersion
        var current = Assembly.GetEntryAssembly()
                              ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                              ?.InformationalVersion;

        if (string.IsNullOrEmpty(current)) return false;

        // Strip "v" prefix and pre-release suffix for numeric comparison
        var latestCore  = StripPreRelease(tagName.TrimStart('v'));
        var currentCore = StripPreRelease(current.TrimStart('v'));

        if (!Version.TryParse(latestCore,  out var latest))  return false;
        if (!Version.TryParse(currentCore, out var running)) return false;

        if (latest > running) return true;

        // Same numeric part — compare pre-release labels as strings (null/empty = stable > alpha/beta)
        if (latest == running)
        {
            var latestPre  = PreReleasePart(tagName.TrimStart('v'));
            var currentPre = PreReleasePart(current.TrimStart('v'));

            // If the latest has no pre-release tag and current does, latest is newer (stable > pre-release)
            if (string.IsNullOrEmpty(latestPre) && !string.IsNullOrEmpty(currentPre)) return true;
        }

        return false;
    }

    private static string StripPreRelease(string version)
    {
        var dash = version.IndexOf('-');
        return dash < 0 ? version : version[..dash];
    }

    private static string PreReleasePart(string version)
    {
        var dash = version.IndexOf('-');
        return dash < 0 ? string.Empty : version[(dash + 1)..];
    }

    // ── GitHub API DTOs ───────────────────────────────────────────────────────

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")] public string  TagName  { get; set; } = string.Empty;
        [JsonPropertyName("name")]     public string  Name     { get; set; } = string.Empty;
        [JsonPropertyName("html_url")] public string  HtmlUrl  { get; set; } = string.Empty;
        [JsonPropertyName("body")]     public string? Body     { get; set; }
        [JsonPropertyName("assets")]   public List<GitHubAsset> Assets { get; set; } = [];
    }

    private sealed class GitHubAsset
    {
        [JsonPropertyName("name")]                 public string Name                { get; set; } = string.Empty;
        [JsonPropertyName("browser_download_url")] public string BrowserDownloadUrl { get; set; } = string.Empty;
    }
}
