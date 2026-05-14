using Microsoft.EntityFrameworkCore;
using SietchConsole.Core.Interfaces;
using SietchConsole.Core.Models;
using SietchConsole.Data.Database;

namespace SietchConsole.Data.Repositories;

public class BackupRecordRepository : IBackupRecordRepository
{
    private readonly SietchConsoleDbContext _db;

    public BackupRecordRepository(SietchConsoleDbContext db) => _db = db;

    public async Task<IReadOnlyList<BackupRecord>> GetAllAsync()
        => await _db.BackupRecords
                    .OrderByDescending(r => r.CreatedAt)
                    .ToListAsync();

    public async Task<BackupRecord?> GetByIdAsync(int id)
        => await _db.BackupRecords.FindAsync(id);

    public async Task<BackupRecord> AddAsync(BackupRecord record)
    {
        _db.BackupRecords.Add(record);
        await _db.SaveChangesAsync();
        return record;
    }

    public async Task UpdateAsync(BackupRecord record)
    {
        _db.BackupRecords.Update(record);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var record = await _db.BackupRecords.FindAsync(id);
        if (record is not null)
        {
            _db.BackupRecords.Remove(record);
            await _db.SaveChangesAsync();
        }
    }
}
