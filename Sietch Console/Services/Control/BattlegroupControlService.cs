using System.Diagnostics;
using System.Management;
using System.Text;
using SietchConsole.Core.Exceptions;
using SietchConsole.Core.Interfaces;
using SietchConsole.Core.Models;

namespace Sietch_Console.Services.Control;

public class BattlegroupControlService : IBattlegroupControlService
{
    private readonly IServerProcessService _processService;
    private readonly ISshService           _sshService;

    public BattlegroupControlService(IServerProcessService processService,
                                     ISshService           sshService)
    {
        _processService = processService;
        _sshService     = sshService;
    }

    // Hyper-V Msvm_ComputerSystem EnabledState codes
    private const int VmStateRunning  = 2;
    private const int VmStateOff      = 3;
    private const int VmStateStarting = 10;
    private const int VmStateStopping = 9;
    private const int VmStateSaved    = 32768;
    private const int VmStatePaused   = 32769;

    // ── Status ──────────────────────────────────────────────────────────────

    public Task<BattlegroupRuntimeStatus> GetStatusAsync(BattlegroupProfile profile)
    {
        return Task.Run(() =>
        {
            // Pod monitor state is always most authoritative
            if (_processService.IsRunning)
                return _processService.IsServerReady
                    ? BattlegroupRuntimeStatus.Running
                    : BattlegroupRuntimeStatus.Starting;

            // Fall back to VM WMI state when pods are not being monitored
            try
            {
                if (!string.IsNullOrWhiteSpace(profile.VmName))
                {
                    var vmStatus = QueryVmState(profile.VmName);
                    if (vmStatus != BattlegroupRuntimeStatus.Unknown)
                        return vmStatus;
                }
            }
            catch (ManagementException ex) when (ex.ErrorCode == ManagementStatus.AccessDenied)
            {
                throw new HyperVException(HyperVErrorCode.AccessDenied,
                    "Access denied querying Hyper-V.", ex);
            }
            catch (ManagementException ex)
            {
                throw new HyperVException(HyperVErrorCode.WmiQueryFailed,
                    $"WMI error: {ex.Message}", ex);
            }

            return BattlegroupRuntimeStatus.Offline;
        });
    }

    // ── Resource utilization ─────────────────────────────────────────────────

    public Task<VmResourceSnapshot?> GetVmResourcesAsync(BattlegroupProfile profile)
    {
        return Task.Run<VmResourceSnapshot?>(() =>
        {
            if (string.IsNullOrWhiteSpace(profile.VmName))
                return null;
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    @"root\virtualization\v2",
                    "SELECT ProcessorLoad, MemoryUsage FROM Msvm_SummaryInformation " +
                    $"WHERE ElementName = '{EscapeWql(profile.VmName)}'");
                foreach (ManagementObject obj in searcher.Get())
                    return new VmResourceSnapshot(
                        Convert.ToInt32(obj["ProcessorLoad"]),
                        Convert.ToInt64(obj["MemoryUsage"]));
            }
            catch { }
            return null;
        });
    }

    // ── Provisioning ─────────────────────────────────────────────────────────

    private void ProvisionVmIfNeeded(BattlegroupProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.VmName))
            throw new HyperVException(HyperVErrorCode.VmNotFound,
                "No VM name is configured in the battleground profile.");

        if (QueryVmState(profile.VmName) != BattlegroupRuntimeStatus.Unknown)
            return;

        var vSwitch  = string.IsNullOrWhiteSpace(profile.VirtualSwitchName)
            ? "Default Switch" : profile.VirtualSwitchName;
        var memBytes = profile.MemoryMb * 1024L * 1024L;
        var cpu      = Math.Max(1, profile.CpuCount);

        var script = $"""
            $ErrorActionPreference = 'Stop'
            New-VM -Name '{EscapePs(profile.VmName)}' `
                   -Generation 2 `
                   -MemoryStartupBytes {memBytes} `
                   -SwitchName '{EscapePs(vSwitch)}'
            Set-VMProcessor -VMName '{EscapePs(profile.VmName)}' -Count {cpu}
            Set-VMMemory    -VMName '{EscapePs(profile.VmName)}' -DynamicMemoryEnabled $false
            """;

        var (exit, _, stderr) = RunPs(script);
        if (exit != 0)
        {
            var code = stderr.Contains("access", StringComparison.OrdinalIgnoreCase)
                ? HyperVErrorCode.AccessDenied : HyperVErrorCode.ProvisioningFailed;
            throw new HyperVException(code, $"Failed to create VM '{profile.VmName}': {stderr.Trim()}");
        }
    }

    // ── Start ─────────────────────────────────────────────────────────────────

    public Task StartAsync(BattlegroupProfile profile) => Task.Run(async () =>
    {
        // 1. Ensure the Hyper-V VM is running
        if (!string.IsNullOrWhiteSpace(profile.VmName))
        {
            ProvisionVmIfNeeded(profile);

            if (QueryVmState(profile.VmName) != BattlegroupRuntimeStatus.Running)
            {
                var (exit, _, stderr) = RunPs($"Start-VM -Name '{EscapePs(profile.VmName)}'");
                if (exit != 0)
                    throw new HyperVException(
                        stderr.Contains("access", StringComparison.OrdinalIgnoreCase)
                            ? HyperVErrorCode.AccessDenied
                            : HyperVErrorCode.WmiQueryFailed,
                        $"Start-VM failed: {stderr.Trim()}");

                await WaitForVmStateAsync(profile.VmName,
                    BattlegroupRuntimeStatus.Running, TimeSpan.FromSeconds(90));
            }
        }

        // 2. Connect SSH to the VM (allow extra time for the guest to finish booting)
        if (!string.IsNullOrWhiteSpace(profile.VmIpAddress))
        {
            // Use the profile's key path if set; fall back to the default location that
            // Funcom's initial-setup.ps1 always writes to (%LOCALAPPDATA%\DuneAwakeningServer\sshKey).
            var keyPath = string.IsNullOrWhiteSpace(profile.VmSshKeyPath)
                ? BattlegroupProfile.DefaultSshKeyPath
                : profile.VmSshKeyPath;

            if (!_sshService.IsConnected)
            {
                // Give the guest OS a moment to start sshd after the VM reaches Running
                await Task.Delay(5_000);
                await _sshService.ConnectAsync(
                    profile.VmIpAddress,
                    profile.VmSshPort,
                    profile.VmUsername,
                    keyPath);
            }
        }

        // 3. Tell the pod monitor to begin tracking pods
        await _processService.StartAsync(profile);
    });

    // ── Stop ──────────────────────────────────────────────────────────────────

    public Task StopAsync(BattlegroupProfile profile) => Task.Run(async () =>
    {
        // Stop pod monitoring (and scale down pods via kubectl)
        if (_processService.IsRunning)
            await _processService.StopAsync(TimeSpan.FromSeconds(30));

        // Disconnect SSH
        if (_sshService.IsConnected)
            await _sshService.DisconnectAsync();

        // Stop the Hyper-V VM
        if (string.IsNullOrWhiteSpace(profile.VmName)) return;

        var current = QueryVmState(profile.VmName);
        if (current is BattlegroupRuntimeStatus.Offline or BattlegroupRuntimeStatus.Unknown)
            return;

        RunPs($"Stop-VM -Name '{EscapePs(profile.VmName)}'");

        var stopped = await TryWaitForVmStateAsync(
            profile.VmName, BattlegroupRuntimeStatus.Offline, TimeSpan.FromSeconds(30));

        if (!stopped)
        {
            var (exit, _, stderr) = RunPs($"Stop-VM -Name '{EscapePs(profile.VmName)}' -Force");
            if (exit != 0)
                throw new HyperVException(HyperVErrorCode.WmiQueryFailed,
                    $"Stop-VM -Force failed: {stderr.Trim()}");
        }
    });

    // ── Restart ───────────────────────────────────────────────────────────────

    public async Task RestartAsync(BattlegroupProfile profile)
    {
        await StopAsync(profile);
        await Task.Delay(2_000);
        await StartAsync(profile);
    }

    // ── UI shortcuts ──────────────────────────────────────────────────────────

    public void OpenControlInterface(BattlegroupProfile profile)
    {
        // Opens the Battlegroup Director web UI.
        // The Director listens on a dynamic NodePort (detected at runtime via kubectl).
        // We open the file browser (port 18888) as a fallback when the dynamic port
        // is not yet known — it is always available when the VM is running.
        var ip = profile.VmIpAddress ?? profile.LocalIpAddress ?? "localhost";

        // Attempt to resolve the Director's NodePort via SSH; fall back to file browser.
        string url;
        if (_sshService.IsConnected)
        {
            var result = _sshService.ExecuteAsync(
                "sudo kubectl get svc -A -o jsonpath=" +
                "'{.items[*].spec.ports[?(@.port==11717)].nodePort}'",
                CancellationToken.None).GetAwaiter().GetResult();

            if (result.Success &&
                int.TryParse(result.Output.Trim().Trim('\''), out var directorPort) &&
                directorPort > 0)
            {
                url = $"http://{ip}:{directorPort}/";
            }
            else
            {
                url = $"http://{ip}:18888/";
            }
        }
        else
        {
            url = $"http://{ip}:18888/";
        }

        Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
    }

    public void OpenFileBrowser(BattlegroupProfile profile)
    {
        // Opens the in-VM file browser served by the battlegroup at port 18888.
        var ip  = profile.VmIpAddress ?? profile.LocalIpAddress ?? "localhost";
        var url = $"http://{ip}:18888/";
        Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
    }

    public void OpenVmShell(BattlegroupProfile profile)
    {
        if (!string.IsNullOrWhiteSpace(profile.VmName))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName        = "vmconnect.exe",
                Arguments       = $"localhost \"{profile.VmName.Replace("\"", "\\\"")}\"",
                UseShellExecute = false,
            });
        }
        else
        {
            Process.Start(new ProcessStartInfo
            {
                FileName        = "powershell.exe",
                UseShellExecute = false,
            });
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static BattlegroupRuntimeStatus QueryVmState(string vmName)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\virtualization\v2",
                "SELECT EnabledState FROM Msvm_ComputerSystem " +
                $"WHERE Caption = 'Virtual Machine' AND ElementName = '{EscapeWql(vmName)}'");
            foreach (ManagementObject obj in searcher.Get())
            {
                return Convert.ToInt32(obj["EnabledState"]) switch
                {
                    VmStateRunning  => BattlegroupRuntimeStatus.Running,
                    VmStateStarting => BattlegroupRuntimeStatus.Starting,
                    VmStateStopping => BattlegroupRuntimeStatus.Stopping,
                    VmStateOff      => BattlegroupRuntimeStatus.Offline,
                    VmStateSaved    => BattlegroupRuntimeStatus.Offline,
                    VmStatePaused   => BattlegroupRuntimeStatus.Offline,
                    _               => BattlegroupRuntimeStatus.Unknown,
                };
            }
        }
        catch { }
        return BattlegroupRuntimeStatus.Unknown;
    }

    private static async Task WaitForVmStateAsync(
        string vmName, BattlegroupRuntimeStatus target, TimeSpan timeout)
    {
        if (!await TryWaitForVmStateAsync(vmName, target, timeout))
            throw new HyperVException(HyperVErrorCode.OperationTimedOut,
                $"VM '{vmName}' did not reach '{target}' within {timeout.TotalSeconds:0} s.");
    }

    private static async Task<bool> TryWaitForVmStateAsync(
        string vmName, BattlegroupRuntimeStatus target, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(2_000);
            if (QueryVmState(vmName) == target) return true;
        }
        return false;
    }

    private static (int exitCode, string stdout, string stderr) RunPs(string script)
    {
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        using var p = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName               = "powershell.exe",
                Arguments              = $"-NonInteractive -NoProfile -EncodedCommand {encoded}",
                UseShellExecute        = false,
                CreateNoWindow         = true,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
            }
        };
        p.Start();
        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        if (!p.WaitForExit(60_000))
        {
            p.Kill();
            return (-1, stdout, "PowerShell timed out after 60 seconds.");
        }
        return (p.ExitCode, stdout, stderr);
    }

    private static string EscapePs(string value)  => value.Replace("'", "''");
    private static string EscapeWql(string value) => value.Replace("'", "''");
}
