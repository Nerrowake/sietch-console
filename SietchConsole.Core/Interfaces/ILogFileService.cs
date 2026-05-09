using SietchConsole.Core.Models;

namespace SietchConsole.Core.Interfaces;

public interface ILogFileService
{
    string? LocateLogDirectory(BattlegroupProfile profile);
    IReadOnlyList<string> GetLogFiles(BattlegroupProfile profile);
    Task<(IReadOnlyList<LogEntry> Entries, long NewOffset)> ReadFromOffsetAsync(string filePath, long offset);
    Task<IReadOnlyList<LogEntry>> ReadAllAsync(string filePath);
    LogEntry? ParseLine(string line);
}
