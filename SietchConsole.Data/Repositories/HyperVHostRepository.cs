using Microsoft.EntityFrameworkCore;
using SietchConsole.Core.Interfaces;
using SietchConsole.Core.Models;
using SietchConsole.Data.Database;

namespace SietchConsole.Data.Repositories;

public class HyperVHostRepository : IHyperVHostRepository
{
    private readonly SietchConsoleDbContext _db;

    public HyperVHostRepository(SietchConsoleDbContext db) => _db = db;

    public async Task<IReadOnlyList<HyperVHost>> GetAllAsync()
        => await _db.HyperVHosts
                    .OrderBy(h => h.IsLocal ? 0 : 1)
                    .ThenBy(h => h.Name)
                    .ToListAsync();

    public async Task<HyperVHost?> GetByIdAsync(string id)
        => await _db.HyperVHosts.FindAsync(id);

    public async Task AddAsync(HyperVHost host)
    {
        _db.HyperVHosts.Add(host);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(HyperVHost host)
    {
        _db.HyperVHosts.Update(host);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(string id)
    {
        var host = await _db.HyperVHosts.FindAsync(id);
        if (host is not null)
        {
            _db.HyperVHosts.Remove(host);
            await _db.SaveChangesAsync();
        }
    }
}
