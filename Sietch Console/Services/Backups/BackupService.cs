using System.Globalization;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SietchConsole.Core.Interfaces;
using SietchConsole.Core.Models;

namespace Sietch_Console.Services.Backups;

public class BackupService : IBackupService
{
    private readonly IConfigurationService _configService;
    private readonly IServiceScopeFactory  _scopeFactory;

    private static readonly JsonSerializerOptions JsonOpts =
        new() { WriteIndented = true };

    public BackupService(IConfigurationService configService, IServiceScopeFactory scopeFactory)
    {
        _configService = configService;
        _scopeFactory  = scopeFactory;
    }

    // #76 – Storage structure
    public string GetBackupBasePath()
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SietchConsole", "Backups");
        Directory.CreateDirectory(path);
        return path;
    }

    public string GetProfileBackupPath(BattlegroupProfile profile)
    {
        var path = Path.Combine(GetBackupBasePath(), $"profile_{profile.Id}");
        Directory.CreateDirectory(path);
        return path;
    }

    // #77 – Configuration backup
    public async Task<BackupRecord> CreateConfigBackupAsync(BattlegroupProfile profile, string? notes = null)
    {
        var configDir = _configService.LocateConfigDirectory(profile)
                        ?? throw new InvalidOperationException("Config directory not found.");

        var stamp     = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        var backupDir = Path.Combine(GetProfileBackupPath(profile), $"{stamp}_Config");
        Directory.CreateDirectory(backupDir);

        var files = Directory.GetFiles(configDir, "*.ini", SearchOption.TopDirectoryOnly);
        foreach (var src in files)
            File.Copy(src, Path.Combine(backupDir, Path.GetFileName(src)), overwrite: true);

        await WriteManifestAsync(backupDir, new BackupManifest
        {
            Type            = "Config",
            ProfileId       = profile.Id,
            ProfileName     = profile.Name,
            SourceDirectory = configDir,
            Files           = files.Select(Path.GetFileName).ToList()!,
        });

        return await RecordAsync(profile, "Config", backupDir, notes);
    }

    // #78 – Save data backup (#134)
    public async Task<BackupRecord> CreateSaveDataBackupAsync(BattlegroupProfile profile, string? notes = null)
    {
        var saveDir = LocateSaveDirectory(profile)
                      ?? throw new InvalidOperationException("Save data directory not found.");

        var stamp     = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        var backupDir = Path.Combine(GetProfileBackupPath(profile), $"{stamp}_SaveData");
        Directory.CreateDirectory(backupDir);

        await Task.Run(() => CopyDirectory(saveDir, backupDir));

        await WriteManifestAsync(backupDir, new BackupManifest
        {
            Type            = "SaveData",
            ProfileId       = profile.Id,
            ProfileName     = profile.Name,
            SourceDirectory = saveDir,
            SaveDirectory   = saveDir,
            Files           = [],
        });

        return await RecordAsync(profile, "SaveData", backupDir, notes);
    }

    // #135 – Full backup (config + save data in one operation)
    public async Task<BackupRecord> CreateFullBackupAsync(BattlegroupProfile profile, string? notes = null)
    {
        var configDir = _configService.LocateConfigDirectory(profile);
        var saveDir   = LocateSaveDirectory(profile);

        if (configDir is null && saveDir is null)
            throw new InvalidOperationException(
                "Neither a config directory nor a save data directory was found for this profile.");

        var stamp     = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        var backupDir = Path.Combine(GetProfileBackupPath(profile), $"{stamp}_Full");
        Directory.CreateDirectory(backupDir);

        // Back up config files into a Config/ subdirectory
        var configFiles = new List<string>();
        if (configDir is not null)
        {
            var configBackupDir = Path.Combine(backupDir, "Config");
            Directory.CreateDirectory(configBackupDir);
            foreach (var src in Directory.GetFiles(configDir, "*.ini", SearchOption.TopDirectoryOnly))
            {
                var name = Path.GetFileName(src);
                File.Copy(src, Path.Combine(configBackupDir, name), overwrite: true);
                configFiles.Add(name);
            }
        }

        // Back up save data into a SaveData/ subdirectory
        if (saveDir is not null)
            await Task.Run(() => CopyDirectory(saveDir, Path.Combine(backupDir, "SaveData")));

        await WriteManifestAsync(backupDir, new BackupManifest
        {
            Type            = "Full",
            ProfileId       = profile.Id,
            ProfileName     = profile.Name,
            SourceDirectory = profile.InstallPath,
            ConfigDirectory = configDir,
            SaveDirectory   = saveDir,
            Files           = configFiles,
        });

        return await RecordAsync(profile, "Full", backupDir, notes);
    }

    // #80 – Restore (#137)
    public async Task RestoreAsync(BackupRecord record)
    {
        if (!Directory.Exists(record.BackupPath))
            throw new DirectoryNotFoundException($"Backup folder not found: {record.BackupPath}");

        var manifest = await ReadManifestAsync(record.BackupPath);
        if (manifest is null)
            throw new InvalidOperationException("Backup manifest is missing or corrupt.");

        await Task.Run(() =>
        {
            if (manifest.Type == "Full")
            {
                // Full restore: config files from Config/ subdirectory, save data from SaveData/
                if (manifest.ConfigDirectory is not null)
                {
                    var configBackupDir = Path.Combine(record.BackupPath, "Config");
                    if (Directory.Exists(configBackupDir))
                    {
                        Directory.CreateDirectory(manifest.ConfigDirectory);
                        foreach (var file in manifest.Files)
                        {
                            var src  = Path.Combine(configBackupDir, file);
                            var dest = Path.Combine(manifest.ConfigDirectory, file);
                            if (File.Exists(src)) File.Copy(src, dest, overwrite: true);
                        }
                    }
                }

                if (manifest.SaveDirectory is not null)
                {
                    var saveBackupDir = Path.Combine(record.BackupPath, "SaveData");
                    if (Directory.Exists(saveBackupDir))
                    {
                        Directory.CreateDirectory(manifest.SaveDirectory);
                        CopyDirectory(saveBackupDir, manifest.SaveDirectory, skipManifest: true);
                    }
                }
            }
            else if (manifest.Files.Count > 0)
            {
                // Config restore — copy individual INI files back to their source directory
                if (!Directory.Exists(manifest.SourceDirectory))
                    Directory.CreateDirectory(manifest.SourceDirectory);

                foreach (var file in manifest.Files)
                {
                    var src  = Path.Combine(record.BackupPath, file);
                    var dest = Path.Combine(manifest.SourceDirectory, file);
                    if (File.Exists(src)) File.Copy(src, dest, overwrite: true);
                }
            }
            else
            {
                // SaveData restore — copy entire directory tree
                var target = manifest.SaveDirectory ?? manifest.SourceDirectory;
                if (!Directory.Exists(target))
                    Directory.CreateDirectory(target);
                CopyDirectory(record.BackupPath, target, skipManifest: true);
            }
        });
    }

    // #136 – Prune old backups, keeping at most <keepCount> per profile
    public async Task PruneOldBackupsAsync(BattlegroupProfile profile, int keepCount)
    {
        if (keepCount <= 0) return;

        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IBackupRecordRepository>();
        var all  = await repo.GetAllAsync();

        var toDelete = all
            .Where(b => b.BattlegroupProfileId == profile.Id)
            .OrderByDescending(b => b.CreatedAt)
            .Skip(keepCount)
            .ToList();

        foreach (var old in toDelete)
            await DeleteAsync(old);
    }

    // #81 – Delete
    public async Task DeleteAsync(BackupRecord record)
    {
        await Task.Run(() =>
        {
            if (Directory.Exists(record.BackupPath))
                Directory.Delete(record.BackupPath, recursive: true);
        });

        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IBackupRecordRepository>();
        await repo.DeleteAsync(record.Id);
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private static string? LocateSaveDirectory(BattlegroupProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.InstallPath)) return null;

        var candidates = new[]
        {
            Path.Combine(profile.InstallPath, "Saved", "SaveGames"),
            Path.Combine(profile.InstallPath, "Saved", "Islands"),
            Path.Combine(profile.InstallPath, "Saved"),
            Path.Combine(profile.InstallPath, "save"),
        };

        return candidates.FirstOrDefault(Directory.Exists);
    }

    private async Task<BackupRecord> RecordAsync(
        BattlegroupProfile profile, string type, string backupDir, string? notes)
    {
        var size   = CalculateSize(backupDir);
        var record = new BackupRecord
        {
            BackupType          = type,
            BackupPath          = backupDir,
            SizeBytes           = size,
            Notes               = notes,
            AppVersion          = GetAppVersion(),
            BattlegroupProfileId= profile.Id,
            CreatedAt           = DateTime.UtcNow,
        };

        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IBackupRecordRepository>();
        return await repo.AddAsync(record);
    }

    private static long CalculateSize(string dir)
    {
        try
        {
            return Directory.GetFiles(dir, "*", SearchOption.AllDirectories)
                            .Sum(f => new FileInfo(f).Length);
        }
        catch { return 0; }
    }

    private static string GetAppVersion()
    {
        var v = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version;
        return v is null ? "1.0.0" : $"{v.Major}.{v.Minor}.{v.Build}";
    }

    private static async Task WriteManifestAsync(string dir, BackupManifest manifest)
    {
        manifest.CreatedAt = DateTime.UtcNow;
        var json = JsonSerializer.Serialize(manifest, JsonOpts);
        await File.WriteAllTextAsync(Path.Combine(dir, "manifest.json"), json);
    }

    private static async Task<BackupManifest?> ReadManifestAsync(string dir)
    {
        var path = Path.Combine(dir, "manifest.json");
        if (!File.Exists(path)) return null;
        var json = await File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize<BackupManifest>(json);
    }

    private static void CopyDirectory(string src, string dest, bool skipManifest = false)
    {
        Directory.CreateDirectory(dest);
        foreach (var file in Directory.GetFiles(src))
        {
            if (skipManifest && Path.GetFileName(file) == "manifest.json") continue;
            File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), overwrite: true);
        }
        foreach (var subDir in Directory.GetDirectories(src))
            CopyDirectory(subDir, Path.Combine(dest, Path.GetFileName(subDir)), skipManifest);
    }

    private sealed class BackupManifest
    {
        public string       Type            { get; set; } = string.Empty;
        public int          ProfileId       { get; set; }
        public string       ProfileName     { get; set; } = string.Empty;
        public DateTime     CreatedAt       { get; set; }
        public string       SourceDirectory { get; set; } = string.Empty;
        /// <summary>For Full backups: original config directory path (restored to Config/ subdir).</summary>
        public string?      ConfigDirectory { get; set; }
        /// <summary>For SaveData and Full backups: original save data directory path.</summary>
        public string?      SaveDirectory   { get; set; }
        public List<string> Files           { get; set; } = [];
    }
}
