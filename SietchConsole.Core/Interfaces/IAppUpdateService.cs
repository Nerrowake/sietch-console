using SietchConsole.Core.Models;

namespace SietchConsole.Core.Interfaces;

/// <summary>
/// Checks GitHub Releases for new Sietch Console versions and
/// manages the self-update lifecycle (#145–#148).
/// </summary>
public interface IAppUpdateService
{
    /// <summary>
    /// Query the GitHub Releases API for the latest release.
    /// Returns <c>null</c> if the current version is up to date or the check fails.
    /// </summary>
    Task<AppUpdateInfo?> CheckForUpdateAsync(CancellationToken ct = default);

    /// <summary>
    /// Download the installer binary for <paramref name="info"/> to a temp directory.
    /// Reports download progress (0–1) via <paramref name="progress"/>.
    /// Returns the local path to the downloaded file.
    /// </summary>
    Task<string> DownloadInstallerAsync(
        AppUpdateInfo         info,
        IProgress<double>?    progress = null,
        CancellationToken     ct       = default);

    /// <summary>
    /// Launch the installer at <paramref name="installerPath"/> and shut down the application.
    /// </summary>
    void LaunchInstallerAndExit(string installerPath);
}
