using System.Diagnostics;
using System.IO;
using System.Management;
using SietchConsole.Core.Interfaces;
using SietchConsole.Core.Models;

namespace Sietch_Console.Services.Control;

public class BattlegroupControlService : IBattlegroupControlService
{
    // Known server process names to check when VM name is not set
    private static readonly string[] ServerProcessNames =
        ["DedicatedServer", "DuneServer", "BattlegroupServer"];

    // Hyper-V VM EnabledState codes
    private const int VmStateRunning  = 2;
    private const int VmStateDisabled = 3;
    private const int VmStateStarting = 10;

    // #50 – Detect runtime status
    public Task<BattlegroupRuntimeStatus> GetStatusAsync(BattlegroupProfile profile)
    {
        return Task.Run(() =>
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(profile.VmName))
                {
                    var vmStatus = GetVmStatus(profile.VmName);
                    if (vmStatus != BattlegroupRuntimeStatus.Unknown)
                        return vmStatus;
                }

                // Fall back: check for running server processes
                bool serverRunning = ServerProcessNames
                    .Any(name => Process.GetProcessesByName(name).Length > 0);

                return serverRunning
                    ? BattlegroupRuntimeStatus.Running
                    : BattlegroupRuntimeStatus.Offline;
            }
            catch
            {
                return BattlegroupRuntimeStatus.Unknown;
            }
        });
    }

    private static BattlegroupRuntimeStatus GetVmStatus(string vmName)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\virtualization\v2",
                $"SELECT EnabledState FROM Msvm_ComputerSystem " +
                $"WHERE Caption = 'Virtual Machine' AND ElementName = '{vmName}'");

            foreach (ManagementObject obj in searcher.Get())
            {
                var state = Convert.ToInt32(obj["EnabledState"]);
                return state switch
                {
                    VmStateRunning  => BattlegroupRuntimeStatus.Running,
                    VmStateStarting => BattlegroupRuntimeStatus.Starting,
                    VmStateDisabled => BattlegroupRuntimeStatus.Offline,
                    _               => BattlegroupRuntimeStatus.Unknown,
                };
            }
        }
        catch { }
        return BattlegroupRuntimeStatus.Unknown;
    }

    // #51 – Start
    public Task StartAsync(BattlegroupProfile profile)
    {
        return Task.Run(() =>
        {
            // Start the Hyper-V VM if a VM name is configured
            if (!string.IsNullOrWhiteSpace(profile.VmName))
                RunPowerShell($"Start-VM -Name '{EscapePs(profile.VmName)}'");

            // Launch battlegroup.bat if present
            var script = FindBattlegroupScript(profile.InstallPath);
            if (script is not null)
                LaunchScript(script, Path.GetDirectoryName(script)!);
        });
    }

    // #52 – Stop
    public Task StopAsync(BattlegroupProfile profile)
    {
        return Task.Run(() =>
        {
            // Try a graceful stop script first
            var script = FindBattlegroupScript(profile.InstallPath);
            if (script is not null)
                LaunchScript(script, Path.GetDirectoryName(script)!, "stop");

            // Stop the VM
            if (!string.IsNullOrWhiteSpace(profile.VmName))
                RunPowerShell($"Stop-VM -Name '{EscapePs(profile.VmName)}' -Force");
        });
    }

    // #53 – Restart
    public async Task RestartAsync(BattlegroupProfile profile)
    {
        await StopAsync(profile);
        await Task.Delay(3000); // give services time to shut down
        await StartAsync(profile);
    }

    // #54 – Open official control interface (web admin panel)
    public void OpenControlInterface(BattlegroupProfile profile)
    {
        var url = string.IsNullOrWhiteSpace(profile.LocalIpAddress)
            ? "http://localhost:8080"
            : $"http://{profile.LocalIpAddress}:8080";
        OpenUrl(url);
    }

    // #55 – Open file browser
    public void OpenFileBrowser(BattlegroupProfile profile)
    {
        var path = Directory.Exists(profile.InstallPath)
            ? profile.InstallPath
            : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Process.Start("explorer.exe", path);
    }

    // #56 – Open VM shell (Hyper-V Virtual Machine Connection)
    public void OpenVmShell(BattlegroupProfile profile)
    {
        if (!string.IsNullOrWhiteSpace(profile.VmName))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "vmconnect.exe",
                Arguments = $"localhost \"{profile.VmName}\"",
                UseShellExecute = true,
            });
        }
        else
        {
            // Fall back: open PowerShell in install directory
            Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                WorkingDirectory = Directory.Exists(profile.InstallPath)
                    ? profile.InstallPath
                    : null,
                UseShellExecute = true,
            });
        }
    }

    private static string? FindBattlegroupScript(string installPath)
    {
        if (!Directory.Exists(installPath)) return null;
        var direct = Path.Combine(installPath, "battlegroup.bat");
        if (File.Exists(direct)) return direct;
        return Directory.GetFiles(installPath, "battlegroup.bat", SearchOption.AllDirectories)
                        .FirstOrDefault();
    }

    private static void LaunchScript(string scriptPath, string workingDir, string args = "")
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c \"{scriptPath}\" {args}".TrimEnd(),
            WorkingDirectory = workingDir,
            UseShellExecute = true,
        });
    }

    private static void RunPowerShell(string command)
    {
        using var p = Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NonInteractive -Command \"{command}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
        });
        p?.WaitForExit(30_000);
    }

    private static void OpenUrl(string url)
    {
        Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
    }

    private static string EscapePs(string value) => value.Replace("'", "''");
}
