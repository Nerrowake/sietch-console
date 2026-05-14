using System.IO;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using SietchConsole.Core.Interfaces;
using SietchConsole.Core.Models;

namespace Sietch_Console.Services.Cloud;

/// <summary>
/// Cloud storage provider backed by an S3-compatible service: AWS, Backblaze B2, Cloudflare R2,
/// or any MinIO-compatible endpoint (#176).
/// When <see cref="S3Config.EndpointUrl"/> is set, path-style addressing and the specified
/// region are used so Backblaze / MinIO work without further configuration.
/// </summary>
public sealed class S3StorageProvider : ICloudStorageProvider, IDisposable
{
    private readonly S3Config      _config;
    private readonly AmazonS3Client _client;

    public string ProviderName => "S3";

    public S3StorageProvider(S3Config config)
    {
        _config = config;

        var credentials = new BasicAWSCredentials(config.AccessKeyId, config.SecretKey);

        var s3Config = new AmazonS3Config
        {
            UseAccelerateEndpoint = false,
        };

        if (!string.IsNullOrWhiteSpace(config.EndpointUrl))
        {
            // Non-AWS S3-compatible provider (Backblaze, MinIO, R2)
            s3Config.ServiceURL    = config.EndpointUrl;
            s3Config.ForcePathStyle = true;   // required by Backblaze and MinIO
            // AuthenticationRegion instead of RegionEndpoint when ServiceURL is set
            s3Config.AuthenticationRegion = config.Region ?? "us-east-1";
        }
        else
        {
            s3Config.RegionEndpoint = RegionEndpoint.GetBySystemName(config.Region ?? "us-east-1");
        }

        _client = new AmazonS3Client(credentials, s3Config);
    }

    // ── ICloudStorageProvider ─────────────────────────────────────────────────

    public async Task<string> UploadAsync(
        string fileName, Stream content, long contentLength,
        IProgress<double>? progress = null, CancellationToken ct = default)
    {
        var key = $"{_config.KeyPrefix}/{fileName}".TrimStart('/');

        var request = new PutObjectRequest
        {
            BucketName  = _config.BucketName,
            Key         = key,
            InputStream = content,
        };

        // S3 SDK streams uploads; track progress via the event if a reporter is wired up.
        if (progress is not null && contentLength > 0)
        {
            long uploaded = 0;
            request.StreamTransferProgress += (_, e) =>
            {
                uploaded = e.TransferredBytes;
                progress.Report((double)uploaded / contentLength);
            };
        }

        await _client.PutObjectAsync(request, ct);
        return key;   // S3 uses the key as the remote identifier
    }

    public async Task<Stream> DownloadAsync(string remoteId, CancellationToken ct = default)
    {
        var response = await _client.GetObjectAsync(new GetObjectRequest
        {
            BucketName = _config.BucketName,
            Key        = remoteId,
        }, ct);

        // Copy to MemoryStream so the caller can seek and the response gets disposed cleanly.
        var ms = new MemoryStream();
        await response.ResponseStream.CopyToAsync(ms, ct);
        response.Dispose();
        ms.Seek(0, SeekOrigin.Begin);
        return ms;
    }

    public async Task<IReadOnlyList<CloudBackupFile>> ListAsync(CancellationToken ct = default)
    {
        var prefix  = (_config.KeyPrefix + "/").TrimStart('/');
        var results = new List<CloudBackupFile>();

        string? continuationToken = null;
        do
        {
            var response = await _client.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName         = _config.BucketName,
                Prefix             = prefix,
                ContinuationToken  = continuationToken,
            }, ct);

            foreach (var obj in response.S3Objects)
            {
                if (!obj.Key.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) continue;

                results.Add(new CloudBackupFile
                {
                    RemoteId  = obj.Key,
                    FileName  = Path.GetFileName(obj.Key),
                    SizeBytes = obj.Size,
                    CreatedAt = obj.LastModified.ToUniversalTime(),
                });
            }

            continuationToken = response.IsTruncated == true ? response.NextContinuationToken : null;

        } while (continuationToken is not null);

        return results.OrderByDescending(f => f.CreatedAt).ToList();
    }

    public async Task DeleteAsync(string remoteId, CancellationToken ct = default)
    {
        await _client.DeleteObjectAsync(new DeleteObjectRequest
        {
            BucketName = _config.BucketName,
            Key        = remoteId,
        }, ct);
    }

    public async Task<(bool Success, string? Error)> TestConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            // HeadBucket verifies credentials and bucket existence without listing objects.
            await _client.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = _config.BucketName,
                MaxKeys    = 1,
            }, ct);

            return (true, null);
        }
        catch (AmazonS3Exception ex)
        {
            return (false, $"S3 error {ex.ErrorCode}: {ex.Message}");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    // ── Disposal ──────────────────────────────────────────────────────────────

    public void Dispose() => _client.Dispose();
}

/// <summary>Configuration snapshot passed to <see cref="S3StorageProvider"/> at construction.</summary>
public sealed record S3Config(
    string BucketName,
    string? Region,
    string? EndpointUrl,
    string AccessKeyId,
    string SecretKey,
    string? KeyPrefix);
