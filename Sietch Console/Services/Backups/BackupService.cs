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

    // #78 – Save data backup
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
            Files           = [],
        });

        return await RecordAsync(profile, "SaveData", backupDir, notes);
    }

    // #80 – Restore
    public async Task RestoreAsync(BackupRecord record)
    {
        if (!Directory.Exists(record.BackupPath))
            throw new DirectoryNotFoundException($"Backup folder not found: {record.BackupPath}");

        var manifest = await ReadManifestAsync(record.BackupPath);
        if (manifest is null)
            throw new InvalidOperationException("Backup manifest is missing or corrupt.");

        await Task.Run(() =>
        {
            if (!Directory.Exists(manifest.SourceDirectory))
                Directory.CreateDirectory(manifest.SourceDirectory);

            if (manifest.Files.Count > 0)
            {
                // Config restore — copy individual files
                foreach (var file in manifest.Files)
                {
                    var src  = Path.Combine(record.BackupPath, file);
                    var dest = Path.Combine(manifest.SourceDirectory, file);
                    if (File.Exists(src)) File.Copy(src, dest, overwrite: true);
                }
            }
            else
            {
                // Directory restore — copy entire tree
                CopyDirectory(record.BackupPath, manifest.SourceDirectory,
                    skipManifest: true);
            }
        });
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
        public List<string> Files           { get; set; } = [];
    }
}
