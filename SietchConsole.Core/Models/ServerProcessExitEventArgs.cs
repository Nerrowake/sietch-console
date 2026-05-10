namespace SietchConsole.Core.Models;

/// <summary>
/// Carries the result of a server process exit — whether expected or unexpected.
/// </summary>
public sealed class ServerProcessExitEventArgs : EventArgs
{
    /// <summary>OS-level exit code from the server process.</summary>
    public int ExitCode { get; init; }

    /// <summary>Human-readable description of why the process exited.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// True if the exit was triggered by an explicit <c>StopAsync</c> call;
    /// false if the process exited on its own (clean exit or crash).
    /// </summary>
    public bool WasExpected { get; init; }
}
