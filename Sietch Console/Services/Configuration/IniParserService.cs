using System.IO;
using SietchConsole.Core.Interfaces;
using SietchConsole.Core.Models;

namespace Sietch_Console.Services.Configuration;

public class IniParserService : IIniParserService
{
    public async Task<IniDocument> LoadAsync(string filePath)
    {
        var content = File.Exists(filePath)
            ? await File.ReadAllTextAsync(filePath)
            : string.Empty;
        return IniDocument.Parse(filePath, content);
    }

    public Task SaveAsync(IniDocument document, string? destinationPath = null)
    {
        var path = destinationPath ?? document.FilePath;
        return File.WriteAllTextAsync(path, document.GetRawText());
    }
}
