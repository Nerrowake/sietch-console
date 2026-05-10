namespace SietchConsole.Core.Interfaces;

public interface ISteamCmdService
{
    /// <summary>Returns the path to an existing SteamCMD installation, or null if not found.</summary>
    string? FindSteamCmd();

    /// <summary>
    /// Returns the path to SteamCMD, downloading and self-initializing it into the managed
    /// app-data directory if it is not already present.
    /// </summary>
    Task<string> EnsureSteamCmdAsync(IProgress<string>? progress = null, CancellationToken ct = default);

    /// <summary>
    /// Runs steamcmd.exe with the given arguments, streaming each output line to
    /// <paramref name="onOutput"/>. Returns the process exit code.
    /// </summary>
    Task<int> RunCommandAsync(
        string steamCmdPath,
        string arguments,
        Action<string>? onOutput = null,
        CancellationToken ct = default);
}
