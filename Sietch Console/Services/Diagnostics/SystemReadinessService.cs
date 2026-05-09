using Sietch_Console.Services.Diagnostics.Checks;
using SietchConsole.Core.Interfaces;
using SietchConsole.Core.Models;

namespace Sietch_Console.Services.Diagnostics;

public class SystemReadinessService : ISystemReadinessService
{
    public Task<IReadOnlyList<DiagnosticsResult>> RunAllChecksAsync()
    {
        return Task.Run<IReadOnlyList<DiagnosticsResult>>(() =>
        [
            WindowsVersionCheck.Run(),
            HyperVCheck.Run(),
            VirtualizationCheck.Run(),
            MemoryCheck.Run(),
            DiskSpaceCheck.Run(),
            NetworkCheck.Run(),
            FirewallCheck.Run(),
        ]);
    }
}
