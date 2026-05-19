using SietchConsole.Core.Models;

namespace SietchConsole.Core.Interfaces;

/// <summary>
/// Manages a persistent SSH + SFTP connection to the Hyper-V VM running the battlegroup.
/// Registered as a singleton — connect once per session via BattlegroupControlService.
/// </summary>
public interface ISshService
{
    /// <summary>True while the SSH session is open and authenticated.</summary>
    bool IsConnected { get; }

    /// <summary>Establishes the SSH and SFTP connections using a private key file.</summary>
    Task ConnectAsync(string host, int port, string username, string privateKeyPath,
                      CancellationToken ct = default);

    /// <summary>Closes and disposes both SSH and SFTP connections.</summary>
    Task DisconnectAsync();

    /// <summary>Runs a command and returns its exit code, stdout, and stderr.</summary>
    Task<SshCommandResult> ExecuteAsync(string command, CancellationToken ct = default);

    /// <summary>
    /// Streams stdout from a long-running command line-by-line until cancelled or the
    /// command exits.
    /// </summary>
    IAsyncEnumerable<string> StreamLinesAsync(string command, CancellationToken ct = default);

    /// <summary>Downloads a text file from the VM via SFTP.</summary>
    Task<string> ReadRemoteFileAsync(string remotePath, CancellationToken ct = default);

    /// <summary>Uploads a text file to the VM via SFTP, overwriting if present.</summary>
    Task WriteRemoteFileAsync(string remotePath, string content, CancellationToken ct = default);

    /// <summary>
    /// Opens a throw-away connection, runs <c>echo ok</c>, and reports success or the
    /// exception message.  Does not affect the persistent session.
    /// </summary>
    Task<(bool Success, string? Error)> TestConnectionAsync(
        string host, int port, string username, string privateKeyPath,
        CancellationToken ct = default);
}
