using SietchConsole.Core.Models;

namespace SietchConsole.Core.Interfaces;

/// <summary>
/// Reads and writes battlegroup configuration files inside the Hyper-V VM via SFTP.
/// Used by SettingsViewModel when a <see cref="BattlegroupProfile.RemoteConfigPath"/>
/// is configured and SSH is connected.
/// </summary>
public interface IRemoteConfigService
{
    /// <summary>
    /// Returns true when SSH is connected and a remote config path is configured on
    /// the active profile.  When false, the Settings view falls back to local file I/O.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Lists the names of config files present under <paramref name="remoteDirPath"/>.
    /// Returns an empty collection when the directory is missing or SSH is not connected.
    /// </summary>
    Task<IReadOnlyList<string>> ListConfigFilesAsync(string remoteDirPath,
                                                      CancellationToken ct = default);

    /// <summary>Downloads a config file from the VM via SFTP and returns its text content.</summary>
    Task<string> ReadConfigFileAsync(string remotePath, CancellationToken ct = default);

    /// <summary>Uploads (overwrites) a config file in the VM via SFTP.</summary>
    Task WriteConfigFileAsync(string remotePath, string content, CancellationToken ct = default);
}
