namespace SietchConsole.Core.Models;

/// <summary>A backup file stored in a remote cloud provider.</summary>
public sealed record CloudBackupFile
{
    /// <summary>Provider-specific identifier (OneDrive item ID or S3 object key).</summary>
    public string RemoteId { get; init; } = string.Empty;

    /// <summary>File name as stored in the cloud (e.g. "backup_1_20260513_143022_Full.zip").</summary>
    public string FileName { get; init; } = string.Empty;

    /// <summary>File size in bytes as reported by the provider.</summary>
    public long SizeBytes { get; init; }

    /// <summary>Creation timestamp reported by the provider.</summary>
    public DateTime CreatedAt { get; init; }
}
