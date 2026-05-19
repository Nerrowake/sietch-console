using SietchConsole.Core.Models;

namespace SietchConsole.Core.Interfaces;

public interface IBattlegroupControlService
{
    Task<BattlegroupRuntimeStatus> GetStatusAsync(BattlegroupProfile profile);
    Task<VmResourceSnapshot?> GetVmResourcesAsync(BattlegroupProfile profile);
    Task StartAsync(BattlegroupProfile profile);
    Task StopAsync(BattlegroupProfile profile);
    Task RestartAsync(BattlegroupProfile profile);

    /// <summary>
    /// Issues <c>battlegroup update</c> inside the VM to pull the latest server build via SteamCMD.
    /// Streams output lines to <paramref name="onOutput"/> as they arrive.
    /// Throws <see cref="InvalidOperationException"/> when SSH is not connected or the command fails.
    /// </summary>
    Task UpdateBattlegroupAsync(BattlegroupProfile profile, Action<string> onOutput,
                                CancellationToken ct = default);

    /// <summary>
    /// Issues <c>battlegroup enable-experimental-swap</c> inside the VM.
    /// Should only be called while the battlegroup is stopped.
    /// Returns <c>(true, null)</c> on success, <c>(false, errorMessage)</c> on failure.
    /// </summary>
    Task<(bool Success, string? Error)> EnableExperimentalSwapAsync(BattlegroupProfile profile,
                                                                     CancellationToken ct = default);

    /// <summary>
    /// Issues <c>battlegroup backup</c> inside the VM to create a server-managed archive.
    /// Streams output lines to <paramref name="onOutput"/> as they arrive.
    /// Returns the VM-side archive path on success, or throws on failure.
    /// </summary>
    Task<string> BackupBattlegroupAsync(BattlegroupProfile profile,
                                        Action<string>? onOutput = null,
                                        CancellationToken ct = default);

    /// <summary>
    /// Issues <c>battlegroup import &lt;vmArchivePath&gt;</c> inside the VM to restore
    /// a previously created archive.
    /// Streams output lines to <paramref name="onOutput"/> as they arrive.
    /// Throws on failure.
    /// </summary>
    Task ImportBattlegroupAsync(BattlegroupProfile profile,
                                string vmArchivePath,
                                Action<string>? onOutput = null,
                                CancellationToken ct = default);

    void OpenControlInterface(BattlegroupProfile profile);
    void OpenFileBrowser(BattlegroupProfile profile);
    void OpenVmShell(BattlegroupProfile profile);
}
