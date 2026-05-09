using Microsoft.EntityFrameworkCore;
using SietchConsole.Core.Interfaces;
using SietchConsole.Core.Models;
using SietchConsole.Data.Database;

namespace SietchConsole.Data.Repositories;

public class ApplicationSettingsRepository : IApplicationSettingsRepository
{
    private readonly SietchConsoleDbContext _db;

    public ApplicationSettingsRepository(SietchConsoleDbContext db) => _db = db;

    public async Task<ApplicationSettings> GetAsync()
    {
        var settings = await _db.ApplicationSettings.FirstOrDefaultAsync();

        if (settings is null)
        {
            settings = new ApplicationSettings();
            _db.ApplicationSettings.Add(settings);
            await _db.SaveChangesAsync();
        }

        return settings;
    }

    public async Task SaveAsync(ApplicationSettings settings)
    {
        settings.UpdatedAt = DateTime.UtcNow;
        _db.ApplicationSettings.Update(settings);
        await _db.SaveChangesAsync();
    }
}
