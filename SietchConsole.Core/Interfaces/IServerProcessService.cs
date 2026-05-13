using SietchConsole.Core.Models;

namespace SietchConsole.Core.Interfaces;

/// <summary>
/// Manages the lifecycle of the Dune: Awakening dedicated server process.
/// Streams stdout/stderr output and tracks server-ready state.
/// </summary>
public interface IServerProcessService
{
    // ── State ────────────────────────────────────────────────────────────────

    /// <summary>True while the server process is alive (started but not yet exited).</summary>
    bool IsRunning { get; }

    /// <summary>
    /// True once a server-ready signal has been detected in the process output
    /// (e.g. "listening on port").  Resets to false on the next start.
    /// </summary>
    bool IsServerReady { get; }

    /// <summary>Exit code from the last completed run, or null if never started or still running.</summary>
    int? LastExitCode { get; }

    /// <summary>Human-readable description of why the process last exited.</summary>
    string? LastExitDescription { get; }

    // ── Events ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Fired for every line received from the server's stdout or stderr.
    /// Raised on a thread-pool thread — callers must marshal to the UI thread if needed.
    /// </summary>
    event EventHandler<string>? OutputLineReceived;

    /// <summary>
    /// Fired when the server process exits (expected stop or unexpected crash).
    /// Raised on a thread-pool thread.
    /// </summary>
    event EventHandler<ServerProcessExitEventArgs>? ProcessExited;

    // ── Control ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Locates the server executable inside <paramref name="profile"/>.InstallPath and starts it.
    /// No-op if the process is already running.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no server executable is found in the install directory.
    /// </exception>
    Task StartAsync(BattlegroupProfile profile, CancellationToken ct = default);

    /// <summary>
    /// Attempts a graceful shutdown by closing the main window and writing "quit" to stdin.
    /// Falls back to a force-kill after <paramref name="timeout"/> elapses.
    /// </summary>
    Task StopAsync(TimeSpan timeout, CancellationToken ct = default);

    /// <summary>
    /// Writes a command line to the server process stdin.
    /// No-op if the process is not running or stdin is not redirected.
    /// </summary>
    Task SendCommandAsync(string command);
}
