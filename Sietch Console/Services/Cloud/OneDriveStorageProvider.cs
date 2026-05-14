using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Identity.Client;
using SietchConsole.Core.Interfaces;
using SietchConsole.Core.Models;

namespace Sietch_Console.Services.Cloud;

/// <summary>
/// Cloud storage provider backed by Microsoft OneDrive via MSAL public client flow (#175).
/// Authentication uses the system browser (redirect URI: http://localhost).
/// The MSAL token cache is persisted to %LOCALAPPDATA%\SietchConsole\msal_cache.bin
/// protected with DPAPI so the user only needs to sign in once.
/// All file operations use raw Microsoft Graph REST calls to avoid the heavyweight
/// Microsoft.Graph SDK dependency.
/// </summary>
public sealed class OneDriveStorageProvider : ICloudStorageProvider, IDisposable
{
    private const string ClientId      = "ce9a154a-6541-4e91-b91b-869c3862dbfb";
    private const string GraphBase     = "https://graph.microsoft.com/v1.0";
    private const long   ChunkSize     = 10 * 1024 * 1024;   // 10 MB per upload chunk

    private static readonly string[] Scopes = ["Files.ReadWrite", "offline_access"];

    private static readonly string CachePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SietchConsole", "msal_cache.bin");

    private readonly string                    _folderPath;
    private readonly IPublicClientApplication  _msalApp;
    private readonly HttpClient                _http;

    public string ProviderName => "OneDrive";

    public OneDriveStorageProvider(string folderPath)
    {
        _folderPath = folderPath.Trim('/');

        _msalApp = PublicClientApplicationBuilder
            .Create(ClientId)
            .WithAuthority(AzureCloudInstance.AzurePublic, AadAuthorityAudience.PersonalMicrosoftAccount)
            .WithRedirectUri("http://localhost")
            .Build();

        // DPAPI-protected persistent token cache so re-auth is only needed once.
        _msalApp.UserTokenCache.SetBeforeAccess(BeforeAccessNotification);
        _msalApp.UserTokenCache.SetAfterAccess(AfterAccessNotification);

        _http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
    }

    // ── ICloudStorageProvider ─────────────────────────────────────────────────

    public async Task<string> UploadAsync(
        string fileName, Stream content, long contentLength,
        IProgress<double>? progress = null, CancellationToken ct = default)
    {
        var token    = await GetTokenAsync(ct);
        var itemPath = Uri.EscapeDataString($"{_folderPath}/{fileName}");

        if (contentLength <= 4 * 1024 * 1024)
        {
            // Simple PUT for small files (≤ 4 MB)
            using var req = new HttpRequestMessage(
                HttpMethod.Put,
                $"{GraphBase}/me/drive/root:/{itemPath}:/content");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            req.Content               = new StreamContent(content);
            req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            using var resp = await _http.SendAsync(req, ct);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadFromJsonAsync<JsonElement>(ct);
            return json.GetProperty("id").GetString() ?? fileName;
        }

        // Resumable upload for large files (> 4 MB)
        return await UploadLargeFileAsync(token, itemPath, content, contentLength, progress, ct);
    }

    public async Task<Stream> DownloadAsync(string remoteId, CancellationToken ct = default)
    {
        var token = await GetTokenAsync(ct);

        using var req = new HttpRequestMessage(
            HttpMethod.Get,
            $"{GraphBase}/me/drive/items/{remoteId}/content");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // ResponseHeadersRead lets us stream the body without buffering the whole file.
        var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();

        // Copy to a MemoryStream so the response can be disposed independently.
        var ms = new MemoryStream();
        await resp.Content.CopyToAsync(ms, ct);
        ms.Seek(0, SeekOrigin.Begin);
        resp.Dispose();
        return ms;
    }

    public async Task<IReadOnlyList<CloudBackupFile>> ListAsync(CancellationToken ct = default)
    {
        var token    = await GetTokenAsync(ct);
        var encoded  = Uri.EscapeDataString(_folderPath);
        var url      = $"{GraphBase}/me/drive/root:/{encoded}:/children?$select=id,name,size,createdDateTime&$top=1000";

        var results = new List<CloudBackupFile>();

        while (url is not null)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var resp = await _http.SendAsync(req, ct);

            // 404 = folder doesn't exist yet — return empty list
            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                return results;

            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadFromJsonAsync<JsonElement>(ct);

            if (json.TryGetProperty("value", out var items))
            {
                foreach (var item in items.EnumerateArray())
                {
                    var name = item.TryGetProperty("name", out var n) ? n.GetString() : null;
                    if (name is null || !name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var id        = item.TryGetProperty("id",       out var i) ? i.GetString() ?? string.Empty : string.Empty;
                    var size      = item.TryGetProperty("size",      out var s) ? s.GetInt64()  : 0L;
                    var createdAt = item.TryGetProperty("createdDateTime", out var c)
                        ? c.GetDateTime() : DateTime.UtcNow;

                    results.Add(new CloudBackupFile
                    {
                        RemoteId  = id,
                        FileName  = name,
                        SizeBytes = size,
                        CreatedAt = createdAt,
                    });
                }
            }

            // Follow @odata.nextLink for paging
            url = json.TryGetProperty("@odata.nextLink", out var next) ? next.GetString() : null;
        }

        return results.OrderByDescending(f => f.CreatedAt).ToList();
    }

    public async Task DeleteAsync(string remoteId, CancellationToken ct = default)
    {
        var token = await GetTokenAsync(ct);

        using var req = new HttpRequestMessage(
            HttpMethod.Delete,
            $"{GraphBase}/me/drive/items/{remoteId}");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var resp = await _http.SendAsync(req, ct);
        if (resp.StatusCode != System.Net.HttpStatusCode.NoContent)
            resp.EnsureSuccessStatusCode();
    }

    public async Task<(bool Success, string? Error)> TestConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            var token = await GetTokenAsync(ct);

            using var req = new HttpRequestMessage(
                HttpMethod.Get,
                $"{GraphBase}/me/drive?$select=id,driveType");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var resp = await _http.SendAsync(req, ct);
            resp.EnsureSuccessStatusCode();
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    // ── MSAL token acquisition ────────────────────────────────────────────────

    private async Task<string> GetTokenAsync(CancellationToken ct)
    {
        var accounts = await _msalApp.GetAccountsAsync();

        try
        {
            // Silent first — uses cached refresh token when available.
            var result = await _msalApp
                .AcquireTokenSilent(Scopes, accounts.FirstOrDefault())
                .ExecuteAsync(ct);
            return result.AccessToken;
        }
        catch (MsalUiRequiredException)
        {
            // No cached token — open system browser for interactive sign-in.
            var result = await _msalApp
                .AcquireTokenInteractive(Scopes)
                .WithPrompt(Prompt.SelectAccount)
                .ExecuteAsync(ct);
            return result.AccessToken;
        }
    }

    // ── DPAPI token cache persistence ─────────────────────────────────────────

    private static void BeforeAccessNotification(TokenCacheNotificationArgs args)
    {
        if (!File.Exists(CachePath)) return;
        try
        {
            var encrypted = File.ReadAllBytes(CachePath);
            var decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            args.TokenCache.DeserializeMsalV3(decrypted);
        }
        catch { /* Corrupt cache — start fresh */ }
    }

    private static void AfterAccessNotification(TokenCacheNotificationArgs args)
    {
        if (!args.HasStateChanged) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CachePath)!);
            var bytes     = args.TokenCache.SerializeMsalV3();
            var encrypted = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(CachePath, encrypted);
        }
        catch { /* Best-effort — user will re-auth next launch */ }
    }

    // ── Resumable upload (Graph large-file session) ───────────────────────────

    private async Task<string> UploadLargeFileAsync(
        string token, string encodedItemPath, Stream content, long totalSize,
        IProgress<double>? progress, CancellationToken ct)
    {
        // 1. Create upload session
        using var sessionReq = new HttpRequestMessage(
            HttpMethod.Post,
            $"{GraphBase}/me/drive/root:/{encodedItemPath}:/createUploadSession");
        sessionReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        sessionReq.Content = new StringContent(
            "{\"item\":{\"@microsoft.graph.conflictBehavior\":\"replace\"}}",
            Encoding.UTF8, "application/json");

        using var sessionResp = await _http.SendAsync(sessionReq, ct);
        sessionResp.EnsureSuccessStatusCode();
        var sessionJson = await sessionResp.Content.ReadFromJsonAsync<JsonElement>(ct);
        var uploadUrl   = sessionJson.GetProperty("uploadUrl").GetString()!;

        // 2. Upload in chunks
        var buffer    = new byte[ChunkSize];
        long uploaded = 0;

        while (uploaded < totalSize)
        {
            var read = await content.ReadAsync(buffer.AsMemory(0, (int)Math.Min(ChunkSize, totalSize - uploaded)), ct);
            if (read == 0) break;

            var rangeEnd = uploaded + read - 1;

            using var chunkReq = new HttpRequestMessage(HttpMethod.Put, uploadUrl);
            chunkReq.Content = new ByteArrayContent(buffer, 0, read);
            chunkReq.Content.Headers.ContentRange =
                new ContentRangeHeaderValue(uploaded, rangeEnd, totalSize);
            chunkReq.Content.Headers.ContentLength = read;

            using var chunkResp = await _http.SendAsync(chunkReq, ct);

            // 200/201 = final chunk accepted; 202 = more chunks expected
            if (chunkResp.StatusCode != System.Net.HttpStatusCode.Accepted &&
                !chunkResp.IsSuccessStatusCode)
            {
                chunkResp.EnsureSuccessStatusCode();
            }

            uploaded += read;
            progress?.Report((double)uploaded / totalSize);

            // Last chunk: Graph returns the created item JSON
            if (chunkResp.IsSuccessStatusCode && chunkResp.StatusCode != System.Net.HttpStatusCode.Accepted)
            {
                var finalJson = await chunkResp.Content.ReadFromJsonAsync<JsonElement>(ct);
                return finalJson.GetProperty("id").GetString() ?? string.Empty;
            }
        }

        return string.Empty;
    }

    // ── Disposal ──────────────────────────────────────────────────────────────

    public void Dispose() => _http.Dispose();
}
