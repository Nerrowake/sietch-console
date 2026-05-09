using SietchConsole.Core.Models;

namespace SietchConsole.Core.Interfaces;

public interface IBackupService
{
    string GetBackupBasePath();
    string GetProfileBackupPath(BattlegroupProfile profile);
    Task<BackupRecord> CreateConfigBackupAsync(BattlegroupProfile profile, string? notes = null);
    Task<BackupRecord> CreateSaveDataBackupAsync(BattlegroupProfile profile, string? notes = null);
    Task RestoreAsync(BackupRecord record);
    Task DeleteAsync(BackupRecord record);
}
