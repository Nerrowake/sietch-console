using Microsoft.EntityFrameworkCore;
using SietchConsole.Core.Interfaces;
using SietchConsole.Core.Models;
using SietchConsole.Data.Database;

namespace SietchConsole.Data.Repositories;

public class BattlegroupProfileRepository : IBattlegroupProfileRepository
{
    private readonly SietchConsoleDbContext _db;

    public BattlegroupProfileRepository(SietchConsoleDbContext db) => _db = db;

    public async Task<IReadOnlyList<BattlegroupProfile>> GetAllAsync()
        => await _db.BattlegroupProfiles.OrderBy(p => p.Name).ToListAsync();

    public async Task<BattlegroupProfile?> GetByIdAsync(int id)
        => await _db.BattlegroupProfiles.FindAsync(id);

    public async Task<BattlegroupProfile> AddAsync(BattlegroupProfile profile)
    {
        profile.CreatedAt = DateTime.UtcNow;
        profile.UpdatedAt = DateTime.UtcNow;
        _db.BattlegroupProfiles.Add(profile);
        await _db.SaveChangesAsync();
        return profile;
    }

    public async Task UpdateAsync(BattlegroupProfile profile)
    {
        profile.UpdatedAt = DateTime.UtcNow;
        _db.BattlegroupProfiles.Update(profile);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var profile = await _db.BattlegroupProfiles.FindAsync(id);
        if (profile is not null)
        {
            _db.BattlegroupProfiles.Remove(profile);
            await _db.SaveChangesAsync();
        }
    }
}
