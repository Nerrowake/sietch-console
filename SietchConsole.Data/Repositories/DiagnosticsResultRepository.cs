using Microsoft.EntityFrameworkCore;
using SietchConsole.Core.Interfaces;
using SietchConsole.Core.Models;
using SietchConsole.Data.Database;

namespace SietchConsole.Data.Repositories;

public class DiagnosticsResultRepository : IDiagnosticsResultRepository
{
    private readonly SietchConsoleDbContext _db;

    public DiagnosticsResultRepository(SietchConsoleDbContext db) => _db = db;

    public async Task<IReadOnlyList<DiagnosticsResult>> GetRecentAsync(int count = 50)
        => await _db.DiagnosticsResults
                    .OrderByDescending(r => r.CreatedAt)
                    .Take(count)
                    .ToListAsync();

    public async Task AddAsync(DiagnosticsResult result)
    {
        _db.DiagnosticsResults.Add(result);
        await _db.SaveChangesAsync();
    }

    public async Task AddRangeAsync(IEnumerable<DiagnosticsResult> results)
    {
        _db.DiagnosticsResults.AddRange(results);
        await _db.SaveChangesAsync();
    }

    public async Task ClearAsync()
    {
        _db.DiagnosticsResults.RemoveRange(_db.DiagnosticsResults);
        await _db.SaveChangesAsync();
    }
}
