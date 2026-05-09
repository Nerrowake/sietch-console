using SietchConsole.Core.Models;

namespace SietchConsole.Core.Interfaces;

public interface IConfigurationService
{
    /// <summary>Returns the config directory for the profile, or null if it cannot be located.</summary>
    string? LocateConfigDirectory(BattlegroupProfile profile);

    /// <summary>Returns all INI file paths found in the config directory.</summary>
    Task<IReadOnlyList<string>> GetConfigFilesAsync(BattlegroupProfile profile);

    /// <summary>Loads known settings from the profile's config directory into a typed model.</summary>
    Task<BattlegroupConfig?> LoadAsync(BattlegroupProfile profile);

    /// <summary>Returns validation errors. Empty list means the config is valid.</summary>
    IReadOnlyList<string> Validate(BattlegroupConfig config);

    /// <summary>Validates, backs up, and saves config. Returns false if validation fails.</summary>
    Task<bool> SaveAsync(BattlegroupProfile profile, BattlegroupConfig config);

    /// <summary>Returns the raw text content of an INI file.</summary>
    Task<string> GetRawContentAsync(string filePath);

    /// <summary>Writes raw INI content to a file, optionally creating a timestamped backup first.</summary>
    Task SaveRawContentAsync(string filePath, string content, bool createBackup = true);
}
