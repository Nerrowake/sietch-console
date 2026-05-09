using Microsoft.EntityFrameworkCore;
using SietchConsole.Core.Interfaces;
using SietchConsole.Core.Models;
using SietchConsole.Data.Database;

namespace SietchConsole.Data.Repositories;

public class SetupWizardStateRepository : ISetupWizardStateRepository
{
    private readonly SietchConsoleDbContext _db;

    public SetupWizardStateRepository(SietchConsoleDbContext db) => _db = db;

    public async Task<SetupWizardState?> GetActiveAsync()
        => await _db.SetupWizardStates
                    .Where(s => !s.IsComplete)
                    .OrderByDescending(s => s.UpdatedAt)
                    .FirstOrDefaultAsync();

    public async Task<SetupWizardState> SaveAsync(SetupWizardState state)
    {
        state.UpdatedAt = DateTime.UtcNow;
        if (state.Id == 0)
            _db.SetupWizardStates.Add(state);
        else
            _db.SetupWizardStates.Update(state);
        await _db.SaveChangesAsync();
        return state;
    }

    public async Task ClearAsync()
    {
        _db.SetupWizardStates.RemoveRange(_db.SetupWizardStates);
        await _db.SaveChangesAsync();
    }
}
