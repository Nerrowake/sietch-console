using SietchConsole.Core.Models;

namespace SietchConsole.Core.Interfaces;

public interface IBattlegroupControlService
{
    Task<BattlegroupRuntimeStatus> GetStatusAsync(BattlegroupProfile profile);
    Task<VmResourceSnapshot?> GetVmResourcesAsync(BattlegroupProfile profile);
    Task StartAsync(BattlegroupProfile profile);
    Task StopAsync(BattlegroupProfile profile);
    Task RestartAsync(BattlegroupProfile profile);
    void OpenControlInterface(BattlegroupProfile profile);
    void OpenFileBrowser(BattlegroupProfile profile);
    void OpenVmShell(BattlegroupProfile profile);
}
