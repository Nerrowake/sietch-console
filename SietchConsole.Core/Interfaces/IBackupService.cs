using SietchConsole.Core.Models;

namespace SietchConsole.Core.Interfaces;

public interface IBackupService
{
    string GetBackupBasePath();
    string GetProfileBackupPath(BattlegroupProfile profile);

    /// <summary>Back up INI config files only.</summary>
    Task<BackupRecord> CreateConfigBackupAsync(BattlegroupProfile profile, string? notes = null);

    /// <summary>Back up the save-data directory only.</summary>
    Task<BackupRecord> CreateSaveDataBackupAsync(BattlegroupProfile profile, string? notes = null);

    /// <summary>Back up both config files and save data in a single archive (#135).</summary>
    Task<BackupRecord> CreateFullBackupAsync(BattlegroupProfile profile, string? notes = null);

    /// <summary>Restore a previously created backup to its original location.</summary>
    Task RestoreAsync(BackupRecord record);

    /// <summary>Delete a backup record and its files from disk.</summary>
    Task DeleteAsync(BackupRecord record);

    /// <summary>
    /// Delete the oldest backups for <paramref name="profile"/> so that no more than
    /// <paramref name="keepCount"/> backups remain (#136).
    /// </summary>
    Task PruneOldBackupsAsync(BattlegroupProfile profile, int keepCount);
}
