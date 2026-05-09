using SietchConsole.Core.Models;

namespace SietchConsole.Core.Interfaces;

public interface ILogAnalysisService
{
    IReadOnlyList<DetectedIssue> Analyze(IEnumerable<LogEntry> entries);
    string GenerateReport(
        BattlegroupProfile? profile,
        string? logFile,
        IEnumerable<LogEntry> recentEntries,
        IEnumerable<DetectedIssue> issues);
}
