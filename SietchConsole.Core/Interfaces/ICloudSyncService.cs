using SietchConsole.Core.Models;

namespace SietchConsole.Core.Interfaces;

/// <summary>
/// Orchestrates cloud backup sync: zips local backups for upload, downloads and
/// extracts cloud backups for restore, and lists what is stored remotely (#173, #174).
/// </summary>
public interface ICloudSyncService
{
    /// <summary>
    /// True when a cloud provider is selected and enabled in settings.
    /// Commands that require cloud sync should check this before executing.
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Zip <paramref name="record"/>'s local backup folder and upload it to the configured provider.
    /// Returns the remote file identifier assigned by the provider.
    /// Updates <paramref name="record"/> in the database with the remote ID and sync timestamp.
    /// </summary>
    Task<string> UploadBackupAsync(
        BackupRecord record,
        IProgress<double>? progress = null,
        CancellationToken ct = default);

    /// <summary>List all backup ZIP files stored in the configured remote location.</summary>
    Task<IReadOnlyList<CloudBackupFile>> ListCloudBackupsAsync(CancellationToken ct = default);

    /// <summary>
    /// Download <paramref name="file"/> from cloud storage, extract it to a temporary directory,
    /// and restore the backup to its original location via <see cref="IBackupService"/>.
    /// Temporary files are cleaned up regardless of success or failure.
    /// </summary>
    Task DownloadAndRestoreAsync(
        CloudBackupFile file,
        IProgress<double>? progress = null,
        CancellationToken ct = default);

    /// <summary>Delete <paramref name="file"/> from cloud storage.</summary>
    Task DeleteCloudBackupAsync(CloudBackupFile file, CancellationToken ct = default);

    /// <summary>
    /// Test the connection to the configured provider without uploading data.
    /// Returns <c>(true, null)</c> on success.
    /// </summary>
    Task<(bool Success, string? Error)> TestConnectionAsync(CancellationToken ct = default);
}
