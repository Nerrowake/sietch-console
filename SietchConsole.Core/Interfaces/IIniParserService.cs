using SietchConsole.Core.Models;

namespace SietchConsole.Core.Interfaces;

public interface IIniParserService
{
    Task<IniDocument> LoadAsync(string filePath);
    Task SaveAsync(IniDocument document, string? destinationPath = null);
}
