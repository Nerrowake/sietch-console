using System.Diagnostics;
using System.Globalization;
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
        "https://api.github.com/repos/Nerrowake/sietch-console/releases";

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
            var releases = await _http.GetFromJsonAsync<List<GitHubRelease>>(ReleasesApiUrl, ct);
            if (releases is null) return null;

            var release = releases
                .Where(r => !r.Draft)
                .Where(r => IsNewerVersion(r.TagName))
                .FirstOrDefault();

            if (release is null) return null;

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

        var latestSemantic = ParseSemanticVersion(tagName.TrimStart('v'));
        var runningSemantic = ParseSemanticVersion(current.TrimStart('v'));

        return latestSemantic is not null
            && runningSemantic is not null
            && CompareSemanticVersions(latestSemantic, runningSemantic) > 0;

    }

    private sealed record SemanticVersion(Version Core, IReadOnlyList<string> PreReleaseParts);

    private static SemanticVersion? ParseSemanticVersion(string value)
    {
        var buildStart = value.IndexOf('+');
        if (buildStart >= 0)
            value = value[..buildStart];

        var dash = value.IndexOf('-');
        var coreText = dash < 0 ? value : value[..dash];
        var preReleaseText = dash < 0 ? string.Empty : value[(dash + 1)..];

        if (!Version.TryParse(coreText, out var core))
            return null;

        var preReleaseParts = string.IsNullOrWhiteSpace(preReleaseText)
            ? []
            : preReleaseText.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return new SemanticVersion(core, preReleaseParts);
    }

    private static int CompareSemanticVersions(SemanticVersion left, SemanticVersion right)
    {
        var coreComparison = left.Core.CompareTo(right.Core);
        if (coreComparison != 0)
            return coreComparison;

        if (left.PreReleaseParts.Count == 0 && right.PreReleaseParts.Count == 0) return 0;
        if (left.PreReleaseParts.Count == 0) return 1;
        if (right.PreReleaseParts.Count == 0) return -1;

        var sharedLength = Math.Min(left.PreReleaseParts.Count, right.PreReleaseParts.Count);
        for (var i = 0; i < sharedLength; i++)
        {
            var leftPart = left.PreReleaseParts[i];
            var rightPart = right.PreReleaseParts[i];
            var leftIsNumber = int.TryParse(leftPart, NumberStyles.None, CultureInfo.InvariantCulture, out var leftNumber);
            var rightIsNumber = int.TryParse(rightPart, NumberStyles.None, CultureInfo.InvariantCulture, out var rightNumber);

            var partComparison = (leftIsNumber, rightIsNumber) switch
            {
                (true, true) => leftNumber.CompareTo(rightNumber),
                (true, false) => -1,
                (false, true) => 1,
                _ => string.Compare(leftPart, rightPart, StringComparison.OrdinalIgnoreCase),
            };

            if (partComparison != 0)
                return partComparison;
        }

        return left.PreReleaseParts.Count.CompareTo(right.PreReleaseParts.Count);
    }

    // ── GitHub API DTOs ───────────────────────────────────────────────────────

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")] public string  TagName  { get; set; } = string.Empty;
        [JsonPropertyName("name")]     public string  Name     { get; set; } = string.Empty;
        [JsonPropertyName("html_url")] public string  HtmlUrl  { get; set; } = string.Empty;
        [JsonPropertyName("body")]     public string? Body     { get; set; }
        [JsonPropertyName("draft")]    public bool    Draft    { get; set; }
        [JsonPropertyName("assets")]   public List<GitHubAsset> Assets { get; set; } = [];
    }

    private sealed class GitHubAsset
    {
        [JsonPropertyName("name")]                 public string Name                { get; set; } = string.Empty;
        [JsonPropertyName("browser_download_url")] public string BrowserDownloadUrl { get; set; } = string.Empty;
    }
}
