using SietchConsole.Core.Models;

namespace SietchConsole.Core.Interfaces;

public interface IBackupRecordRepository
{
    Task<IReadOnlyList<BackupRecord>> GetAllAsync();
    Task<BackupRecord?> GetByIdAsync(int id);
    Task<BackupRecord> AddAsync(BackupRecord record);
    Task UpdateAsync(BackupRecord record);
    Task DeleteAsync(int id);
}
