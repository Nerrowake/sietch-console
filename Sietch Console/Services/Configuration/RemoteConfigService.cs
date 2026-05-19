using SietchConsole.Core.Interfaces;

namespace Sietch_Console.Services.Configuration;

/// <summary>
/// Reads and writes battlegroup configuration files inside the Hyper-V VM via SFTP.
/// Delegates all transport to the singleton <see cref="ISshService"/>.
/// </summary>
public class RemoteConfigService : IRemoteConfigService
{
    private readonly ISshService _ssh;

    public RemoteConfigService(ISshService ssh) => _ssh = ssh;

    /// <inheritdoc />
    public bool IsAvailable => _ssh.IsConnected;

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> ListConfigFilesAsync(string remoteDirPath,
                                                                   CancellationToken ct = default)
    {
        if (!_ssh.IsConnected || string.IsNullOrWhiteSpace(remoteDirPath))
            return Array.Empty<string>();

        try
        {
            // Run `ls` inside the VM and split on newlines.  We request only filenames
            // (no -l) so the output is clean for parsing.
            var result = await _ssh.ExecuteAsync($"ls \"{remoteDirPath}\"", ct);
            if (!result.Success)
                return Array.Empty<string>();

            return result.Output
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    /// <inheritdoc />
    public Task<string> ReadConfigFileAsync(string remotePath, CancellationToken ct = default)
        => _ssh.ReadRemoteFileAsync(remotePath, ct);

    /// <inheritdoc />
    public Task WriteConfigFileAsync(string remotePath, string content, CancellationToken ct = default)
        => _ssh.WriteRemoteFileAsync(remotePath, content, ct);
}
