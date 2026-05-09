using SietchConsole.Core.Models;

namespace SietchConsole.Core.Interfaces;

public interface IDiagnosticsResultRepository
{
    Task<IReadOnlyList<DiagnosticsResult>> GetRecentAsync(int count = 50);
    Task AddAsync(DiagnosticsResult result);
    Task AddRangeAsync(IEnumerable<DiagnosticsResult> results);
    Task ClearAsync();
}
