namespace SietchConsole.Core.Interfaces;

public interface IServerPackageInstaller
{
    /// <summary>
    /// Downloads and installs the Dune: Awakening dedicated server files to
    /// <paramref name="installPath"/> via SteamCMD.
    /// </summary>
    Task InstallAsync(
        string installPath,
        IProgress<double> progress,
        Action<string> onOutput,
        CancellationToken ct = default);

    /// <summary>
    /// Updates the installed server files to the latest version via SteamCMD.
    /// Safe to call on an existing installation — SteamCMD only downloads changed files.
    /// </summary>
    Task UpdateAsync(
        string installPath,
        IProgress<double> progress,
        Action<string> onOutput,
        CancellationToken ct = default);

    /// <summary>
    /// Compares the installed build ID against the version currently available on Steam.
    /// Returns true if an update is available, false if up-to-date, or null if the check
    /// could not be performed (SteamCMD absent, server not installed, network error).
    /// </summary>
    Task<bool?> CheckForUpdateAsync(string installPath, CancellationToken ct = default);

    /// <summary>
    /// Reads the build ID of the currently installed server from the Steam ACF manifest.
    /// Returns null if the server is not installed or the manifest cannot be read.
    /// </summary>
    string? GetInstalledBuildId(string installPath);

    /// <summary>Checks for required server binaries to confirm the installation is intact.</summary>
    bool VerifyInstallation(string installPath);
}
