using System.Globalization;
using System.IO;
using SietchConsole.Core.Interfaces;
using SietchConsole.Core.Models;

namespace Sietch_Console.Services.Configuration;

public class ConfigurationService : IConfigurationService
{
    private readonly IIniParserService _parser;

    // Maps BattlegroupConfig property names → (relativeFileName, section, key)
    private static readonly Dictionary<string, (string File, string Section, string Key)> Mapping = new()
    {
        [nameof(BattlegroupConfig.ServerName)]            = ("GameUserSettings.ini", "ServerSettings", "ServerName"),
        [nameof(BattlegroupConfig.MaxPlayers)]            = ("GameUserSettings.ini", "ServerSettings", "MaxPlayers"),
        [nameof(BattlegroupConfig.ServerPassword)]        = ("GameUserSettings.ini", "ServerSettings", "ServerPassword"),
        [nameof(BattlegroupConfig.AdminPassword)]         = ("GameUserSettings.ini", "ServerSettings", "AdminPassword"),
        [nameof(BattlegroupConfig.PvPEnabled)]            = ("GameUserSettings.ini", "ServerSettings", "bIsPvP"),
        [nameof(BattlegroupConfig.GamePort)]              = ("Engine.ini",           "URL",            "Port"),
        [nameof(BattlegroupConfig.DayLengthMultiplier)]   = ("Game.ini",             "Gameplay",       "DayLengthMultiplier"),
        [nameof(BattlegroupConfig.NightLengthMultiplier)] = ("Game.ini",             "Gameplay",       "NightLengthMultiplier"),
        [nameof(BattlegroupConfig.MaxTribeMemberCount)]   = ("Game.ini",             "Gameplay",       "MaxTribeMemberCount"),
        [nameof(BattlegroupConfig.ResourceHarvestingRate)]= ("Game.ini",             "Gameplay",       "ResourceHarvestingMultiplier"),
        [nameof(BattlegroupConfig.XpMultiplier)]          = ("Game.ini",             "Gameplay",       "XPMultiplier"),
    };

    public ConfigurationService(IIniParserService parser) => _parser = parser;

    // #59 – Locate config directory
    public string? LocateConfigDirectory(BattlegroupProfile profile)
    {
        // Use explicitly stored path first
        if (!string.IsNullOrWhiteSpace(profile.UserSettingsPath) && Directory.Exists(profile.UserSettingsPath))
            return profile.UserSettingsPath;

        if (string.IsNullOrWhiteSpace(profile.InstallPath) || !Directory.Exists(profile.InstallPath))
            return null;

        // UE5 dedicated server conventional locations
        var candidates = new[]
        {
            Path.Combine(profile.InstallPath, "Saved", "Config", "WindowsServer"),
            Path.Combine(profile.InstallPath, "Saved", "Config", "Windows"),
            Path.Combine(profile.InstallPath, "Config"),
            Path.Combine(profile.InstallPath, "UserSettings"),
        };

        foreach (var dir in candidates)
            if (Directory.Exists(dir)) return dir;

        // Recursive search for GameUserSettings.ini as a fallback
        try
        {
            var found = Directory.GetFiles(profile.InstallPath, "GameUserSettings.ini", SearchOption.AllDirectories)
                                 .FirstOrDefault();
            if (found is not null) return Path.GetDirectoryName(found);
        }
        catch { }

        return null;
    }

    public async Task<IReadOnlyList<string>> GetConfigFilesAsync(BattlegroupProfile profile)
    {
        var dir = LocateConfigDirectory(profile);
        if (dir is null || !Directory.Exists(dir))
            return [];

        return await Task.Run(() =>
            Directory.GetFiles(dir, "*.ini", SearchOption.TopDirectoryOnly)
                     .OrderBy(f => f)
                     .ToList());
    }

    // #60 / #61 – Load known settings
    public async Task<BattlegroupConfig?> LoadAsync(BattlegroupProfile profile)
    {
        var dir = LocateConfigDirectory(profile);
        if (dir is null) return null;

        var config = new BattlegroupConfig();
        var documents = new Dictionary<string, IniDocument>(StringComparer.OrdinalIgnoreCase);

        foreach (var (prop, (file, section, key)) in Mapping)
        {
            var path = Path.Combine(dir, file);
            if (!documents.TryGetValue(file, out var doc))
            {
                doc = await _parser.LoadAsync(path);
                documents[file] = doc;
            }

            var raw = doc.GetValue(section, key);
            if (raw is null) continue;

            ApplyValue(config, prop, raw);
        }

        return config;
    }

    // #64 – Validation rules
    public IReadOnlyList<string> Validate(BattlegroupConfig config)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(config.ServerName))
            errors.Add("Server name is required.");
        else if (config.ServerName.Length > 64)
            errors.Add("Server name must be 64 characters or fewer.");

        if (config.MaxPlayers is < 1 or > 200)
            errors.Add("Max players must be between 1 and 200.");

        if (config.GamePort is < 1024 or > 65535)
            errors.Add("Game port must be between 1024 and 65535.");

        if (config.MaxTribeMemberCount is < 1 or > 500)
            errors.Add("Max tribe member count must be between 1 and 500.");

        if (config.DayLengthMultiplier < 0.1f || config.DayLengthMultiplier > 20f)
            errors.Add("Day length multiplier must be between 0.1 and 20.");

        if (config.NightLengthMultiplier < 0.1f || config.NightLengthMultiplier > 20f)
            errors.Add("Night length multiplier must be between 0.1 and 20.");

        if (config.ResourceHarvestingRate < 0.1f || config.ResourceHarvestingRate > 20f)
            errors.Add("Resource harvesting rate must be between 0.1 and 20.");

        if (config.XpMultiplier < 0.1f || config.XpMultiplier > 20f)
            errors.Add("XP multiplier must be between 0.1 and 20.");

        return errors;
    }

    // #63 / #66 – Save with validation and backup
    public async Task<bool> SaveAsync(BattlegroupProfile profile, BattlegroupConfig config)
    {
        var errors = Validate(config);
        if (errors.Count > 0) return false;

        var dir = LocateConfigDirectory(profile);
        if (dir is null) return false;

        var documents = new Dictionary<string, IniDocument>(StringComparer.OrdinalIgnoreCase);

        // Load all affected documents
        foreach (var (_, (file, _, _)) in Mapping)
        {
            if (!documents.ContainsKey(file))
            {
                var path = Path.Combine(dir, file);
                documents[file] = await _parser.LoadAsync(path);
            }
        }

        // Apply config values
        foreach (var (prop, (file, section, key)) in Mapping)
        {
            var value = GetStringValue(config, prop);
            if (value is null) continue;
            documents[file].SetValue(section, key, value);
        }

        // Backup and save each modified file
        foreach (var (file, doc) in documents)
        {
            var path = Path.Combine(dir, file);
            if (File.Exists(path))
                CreateBackup(path);
            await _parser.SaveAsync(doc);
        }

        return true;
    }

    public Task<string> GetRawContentAsync(string filePath) =>
        File.Exists(filePath)
            ? File.ReadAllTextAsync(filePath)
            : Task.FromResult(string.Empty);

    public async Task SaveRawContentAsync(string filePath, string content, bool createBackup = true)
    {
        if (createBackup && File.Exists(filePath))
            CreateBackup(filePath);
        await File.WriteAllTextAsync(filePath, content);
    }

    // #66 – Create timestamped backup
    private static void CreateBackup(string filePath)
    {
        try
        {
            var dir     = Path.GetDirectoryName(filePath)!;
            var name    = Path.GetFileNameWithoutExtension(filePath);
            var ext     = Path.GetExtension(filePath);
            var stamp   = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            var backup  = Path.Combine(dir, $"{name}_{stamp}{ext}.bak");
            File.Copy(filePath, backup, overwrite: true);
        }
        catch { }
    }

    private static void ApplyValue(BattlegroupConfig config, string prop, string raw)
    {
        switch (prop)
        {
            case nameof(BattlegroupConfig.ServerName):            config.ServerName            = raw; break;
            case nameof(BattlegroupConfig.ServerPassword):        config.ServerPassword        = raw; break;
            case nameof(BattlegroupConfig.AdminPassword):         config.AdminPassword         = raw; break;
            case nameof(BattlegroupConfig.MaxPlayers):            if (int.TryParse(raw, out var mp))   config.MaxPlayers = mp; break;
            case nameof(BattlegroupConfig.GamePort):              if (int.TryParse(raw, out var gp))   config.GamePort = gp; break;
            case nameof(BattlegroupConfig.MaxTribeMemberCount):   if (int.TryParse(raw, out var tm))   config.MaxTribeMemberCount = tm; break;
            case nameof(BattlegroupConfig.PvPEnabled):            config.PvPEnabled            = raw.Equals("true", StringComparison.OrdinalIgnoreCase) || raw == "1"; break;
            case nameof(BattlegroupConfig.DayLengthMultiplier):   if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var dl))  config.DayLengthMultiplier = dl; break;
            case nameof(BattlegroupConfig.NightLengthMultiplier): if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var nl))  config.NightLengthMultiplier = nl; break;
            case nameof(BattlegroupConfig.ResourceHarvestingRate):if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var rh))  config.ResourceHarvestingRate = rh; break;
            case nameof(BattlegroupConfig.XpMultiplier):          if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var xp))  config.XpMultiplier = xp; break;
        }
    }

    private static string? GetStringValue(BattlegroupConfig config, string prop) =>
        prop switch
        {
            nameof(BattlegroupConfig.ServerName)            => config.ServerName,
            nameof(BattlegroupConfig.ServerPassword)        => config.ServerPassword,
            nameof(BattlegroupConfig.AdminPassword)         => config.AdminPassword,
            nameof(BattlegroupConfig.MaxPlayers)            => config.MaxPlayers.ToString(CultureInfo.InvariantCulture),
            nameof(BattlegroupConfig.GamePort)              => config.GamePort.ToString(CultureInfo.InvariantCulture),
            nameof(BattlegroupConfig.MaxTribeMemberCount)   => config.MaxTribeMemberCount.ToString(CultureInfo.InvariantCulture),
            nameof(BattlegroupConfig.PvPEnabled)            => config.PvPEnabled ? "true" : "false",
            nameof(BattlegroupConfig.DayLengthMultiplier)   => config.DayLengthMultiplier.ToString("G", CultureInfo.InvariantCulture),
            nameof(BattlegroupConfig.NightLengthMultiplier) => config.NightLengthMultiplier.ToString("G", CultureInfo.InvariantCulture),
            nameof(BattlegroupConfig.ResourceHarvestingRate)=> config.ResourceHarvestingRate.ToString("G", CultureInfo.InvariantCulture),
            nameof(BattlegroupConfig.XpMultiplier)          => config.XpMultiplier.ToString("G", CultureInfo.InvariantCulture),
            _ => null,
        };
}
