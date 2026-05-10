namespace SietchConsole.Core.Interfaces;

/// <summary>
/// Manages the embedded Kestrel web server used for remote management (#152).
/// Starts and stops on demand; exposes the listen URL and running state.
/// </summary>
public interface IRemoteManagementService
{
    /// <summary>True while the web server is accepting connections.</summary>
    bool IsRunning { get; }

    /// <summary>Port the server is listening on.</summary>
    int Port { get; }

    /// <summary>
    /// The base URL clients should visit, e.g. "http://192.168.1.10:5151".
    /// Null when the server is not running.
    /// </summary>
    string? ListenUrl { get; }

    /// <summary>Raised whenever <see cref="IsRunning"/> or <see cref="ListenUrl"/> changes.</summary>
    event EventHandler? StatusChanged;

    /// <summary>
    /// Starts the embedded web server on the specified port using the given access token.
    /// No-op if already running.
    /// </summary>
    Task StartAsync(int port, string token, CancellationToken ct = default);

    /// <summary>
    /// Stops the embedded web server. No-op if not running.
    /// </summary>
    Task StopAsync(CancellationToken ct = default);
}
