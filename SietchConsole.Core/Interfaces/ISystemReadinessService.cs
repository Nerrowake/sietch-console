using SietchConsole.Core.Models;

namespace SietchConsole.Core.Interfaces;

public interface ISystemReadinessService
{
    Task<IReadOnlyList<DiagnosticsResult>> RunAllChecksAsync();
}
