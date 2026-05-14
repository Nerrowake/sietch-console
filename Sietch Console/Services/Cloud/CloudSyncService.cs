using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SietchConsole.Core.Interfaces;
using SietchConsole.Core.Models;

namespace Sietch_Console.Services.Cloud;

/// <summary>
/// Orchestrates cloud backup sync (#173, #174):
/// - Zips local backup folders for upload
/// - Downloads and extracts cloud backups for restore
/// - Delegates provider-specific I/O to the active ICloudStorageProvider
/// </summary>
public sealed class CloudSyncService : ICloudSyncService
{
    private readonly IServiceScopeFactory       _scopeFactory;
    private readonly IBackupService             _backupService;
    private readonly ILogger<CloudSyncService>  _log;

    private static readonly string TempRoot = Path.Combine(
        Path.GetTempPath(), "SietchConsole", "CloudSync");

    public CloudSyncService(
        IServiceScopeFactory      scopeFactory,
        IBackupService            backupService,
        ILogger<CloudSyncService> log)
    {
        _scopeFactory  = scopeFactory;
        _backupService = backupService;
        _log           = log;
    }

    // ── ICloudSyncService ─────────────────────────────────────────────────────

    public bool IsConfigured
    {
        get
        {
            // Synchronous quick check — load settings inline to avoid async property.
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<IApplicationSettingsRepository>();
                // Fire-and-forget IsConfiguredAsync; fall back to false on exception.
                var settings = repo.GetAsync().GetAwaiter().GetResult();
                return settings.CloudSyncEnabled && settings.CloudSyncProvider != "None";
            }
            catch { return false; }
        }
    }

    public async Task<string> UploadBackupAsync(
        BackupRecord record,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        if (!Directory.Exists(record.BackupPath))
            throw new DirectoryNotFoundException($"Backup folder not found: {record.BackupPath}");

        Directory.CreateDirectory(TempRoot);
        var zipName = BuildZipName(record);
        var tempZip = Path.Combine(TempRoot, zipName);

        try
        {
            // 1. Zip the backup directory
            _log.LogInformation("CloudSync: zipping {Path} → {Zip}", record.BackupPath, tempZip);
            await Task.Run(() => ZipFile.CreateFromDirectory(record.BackupPath, tempZip), ct);

            // 2. Upload
            var provider = await GetProviderAsync(ct);
            long size    = new FileInfo(tempZip).Length;

            string remoteId;
            await using (var fs = new FileStream(tempZip, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                remoteId = await provider.UploadAsync(zipName, fs, size, progress, ct);
            }

            _log.LogInformation("CloudSync: uploaded {File} → remoteId={Id}", zipName, remoteId);

            // 3. Persist sync metadata back to the database
            record.CloudRemoteId = remoteId;
            record.CloudSyncedAt = DateTime.UtcNow;

            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IBackupRecordRepository>();
            await repo.UpdateAsync(record);

            return remoteId;
        }
        finally
        {
            try { if (File.Exists(tempZip)) File.Delete(tempZip); } catch { }
        }
    }

    public async Task<IReadOnlyList<CloudBackupFile>> ListCloudBackupsAsync(CancellationToken ct = default)
    {
        var provider = await GetProviderAsync(ct);
        return await provider.ListAsync(ct);
    }

    public async Task DownloadAndRestoreAsync(
        CloudBackupFile file,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(TempRoot);
        var tempZip    = Path.Combine(TempRoot, $"restore_{Guid.NewGuid():N}.zip");
        var extractDir = Path.Combine(TempRoot, $"restore_{Guid.NewGuid():N}");

        try
        {
            // 1. Download the ZIP
            _log.LogInformation("CloudSync: downloading {File}", file.FileName);
            var provider = await GetProviderAsync(ct);
            using var stream = await provider.DownloadAsync(file.RemoteId, ct);

            progress?.Report(0.33);

            await using (var fs = new FileStream(tempZip, FileMode.Create, FileAccess.Write))
                await stream.CopyToAsync(fs, ct);

            progress?.Report(0.66);

            // 2. Extract
            _log.LogInformation("CloudSync: extracting {Zip} → {Dir}", tempZip, extractDir);
            await Task.Run(() => ZipFile.ExtractToDirectory(tempZip, extractDir, overwriteFiles: true), ct);

            progress?.Report(0.90);

            // 3. Restore using the existing BackupService (reads manifest.json from the extracted dir)
            var tempRecord = new BackupRecord { BackupPath = extractDir };
            await _backupService.RestoreAsync(tempRecord);

            progress?.Report(1.0);
            _log.LogInformation("CloudSync: restore complete from {File}", file.FileName);
        }
        finally
        {
            try { if (File.Exists(tempZip))    File.Delete(tempZip); }           catch { }
            try { if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true); } catch { }
        }
    }

    public async Task DeleteCloudBackupAsync(CloudBackupFile file, CancellationToken ct = default)
    {
        var provider = await GetProviderAsync(ct);
        await provider.DeleteAsync(file.RemoteId, ct);
        _log.LogInformation("CloudSync: deleted remote file {File}", file.FileName);
    }

    public async Task<(bool Success, string? Error)> TestConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            var provider = await GetProviderAsync(ct);
            return await provider.TestConnectionAsync(ct);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Constructs and configures the active ICloudStorageProvider from current settings.
    /// A new instance is created per call so settings changes take effect immediately.
    /// </summary>
    private async Task<ICloudStorageProvider> GetProviderAsync(CancellationToken ct = default)
    {
        ApplicationSettings settings;
        using (var scope = _scopeFactory.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IApplicationSettingsRepository>();
            settings = await repo.GetAsync();
        }

        if (!settings.CloudSyncEnabled || settings.CloudSyncProvider == "None")
            throw new InvalidOperationException("Cloud sync is not enabled. Configure a provider in the Backups view.");

        return settings.CloudSyncProvider switch
        {
            "OneDrive" => new OneDriveStorageProvider(
                settings.CloudSyncFolderPath ?? "SietchConsole/Backups"),

            "S3" => new S3StorageProvider(new S3Config(
                BucketName:  settings.S3BucketName   ?? throw new InvalidOperationException("S3 bucket name is not configured."),
                Region:      settings.S3Region,
                EndpointUrl: settings.S3EndpointUrl,
                AccessKeyId: settings.S3AccessKeyId  ?? throw new InvalidOperationException("S3 access key ID is not configured."),
                SecretKey:   DecryptSecretKey(settings.S3EncryptedSecretKey),
                KeyPrefix:   settings.CloudSyncFolderPath ?? "sietch-console-backups")),

            _ => throw new InvalidOperationException($"Unknown cloud provider: {settings.CloudSyncProvider}"),
        };
    }

    private static string BuildZipName(BackupRecord record) =>
        $"backup_{record.BattlegroupProfileId}_{record.CreatedAt:yyyyMMdd_HHmmss}_{record.BackupType}.zip";

    // ── DPAPI helpers for S3 secret key ──────────────────────────────────────

    public static string EncryptSecretKey(string plaintext)
    {
        var bytes     = Encoding.UTF8.GetBytes(plaintext);
        var encrypted = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(encrypted);
    }

    private static string DecryptSecretKey(string? encryptedBase64)
    {
        if (string.IsNullOrWhiteSpace(encryptedBase64))
            throw new InvalidOperationException("S3 secret key is not configured.");
        try
        {
            var encrypted = Convert.FromBase64String(encryptedBase64);
            var bytes     = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to decrypt the S3 secret key. Re-enter your credentials.", ex);
        }
    }
}
