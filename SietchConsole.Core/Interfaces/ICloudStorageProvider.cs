using SietchConsole.Core.Models;

namespace SietchConsole.Core.Interfaces;

/// <summary>
/// Abstraction over a cloud storage back-end (OneDrive or S3-compatible).
/// All operations act on the configured remote folder / bucket.
/// </summary>
public interface ICloudStorageProvider
{
    /// <summary>Human-readable provider name: "OneDrive" or "S3".</summary>
    string ProviderName { get; }

    /// <summary>
    /// Upload a stream as a new file in the remote folder.
    /// Returns the provider-specific remote identifier used for download/delete.
    /// </summary>
    Task<string> UploadAsync(
        string fileName,
        Stream content,
        long contentLength,
        IProgress<double>? progress = null,
        CancellationToken ct = default);

    /// <summary>Open a readable stream for the file identified by <paramref name="remoteId"/>.</summary>
    Task<Stream> DownloadAsync(string remoteId, CancellationToken ct = default);

    /// <summary>List all backup files stored in the remote folder / bucket prefix.</summary>
    Task<IReadOnlyList<CloudBackupFile>> ListAsync(CancellationToken ct = default);

    /// <summary>Delete the file identified by <paramref name="remoteId"/>.</summary>
    Task DeleteAsync(string remoteId, CancellationToken ct = default);

    /// <summary>
    /// Verify credentials and connectivity without uploading any data.
    /// Returns <c>(true, null)</c> on success, or <c>(false, errorMessage)</c> on failure.
    /// </summary>
    Task<(bool Success, string? Error)> TestConnectionAsync(CancellationToken ct = default);
}
