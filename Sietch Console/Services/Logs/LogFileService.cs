using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using SietchConsole.Core.Interfaces;
using SietchConsole.Core.Models;

namespace Sietch_Console.Services.Logs;

public class LogFileService : ILogFileService
{
    // UE5 format: [2024.01.15-12.30.45:123][  0]LogCategory: Verbosity: Message
    private static readonly Regex Ue5Pattern = new(
        @"^\[(\d{4}\.\d{2}\.\d{2}-\d{2}\.\d{2}\.\d{2}:\d+)\]\[\s*\d+\](\w+):\s*(?:(Display|Warning|Error|Fatal|Verbose|VeryVerbose):\s*)?(.+)$",
        RegexOptions.Compiled);

    // Simple format: [HH:mm:ss] LEVEL message
    private static readonly Regex SimplePattern = new(
        @"^\[(\d{2}:\d{2}:\d{2})\]\s*(ERROR|WARN|WARNING|INFO|DEBUG|TRACE)?\s*(.+)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // #68 – Locate log directory
    public string? LocateLogDirectory(BattlegroupProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.InstallPath) || !Directory.Exists(profile.InstallPath))
            return null;

        var candidates = new[]
        {
            Path.Combine(profile.InstallPath, "Saved", "Logs"),
            Path.Combine(profile.InstallPath, "logs"),
            Path.Combine(profile.InstallPath, "Logs"),
            profile.InstallPath,
        };

        foreach (var dir in candidates)
            if (Directory.Exists(dir) && Directory.GetFiles(dir, "*.log").Length > 0)
                return dir;

        return null;
    }

    public IReadOnlyList<string> GetLogFiles(BattlegroupProfile profile)
    {
        var dir = LocateLogDirectory(profile);
        if (dir is null) return [];

        try
        {
            return Directory.GetFiles(dir, "*.log", SearchOption.TopDirectoryOnly)
                            .OrderByDescending(File.GetLastWriteTime)
                            .ToList();
        }
        catch { return []; }
    }

    // #69 – Tail new entries from a byte offset
    public async Task<(IReadOnlyList<LogEntry> Entries, long NewOffset)> ReadFromOffsetAsync(
        string filePath, long offset)
    {
        if (!File.Exists(filePath)) return ([], offset);

        try
        {
            await using var stream = new FileStream(
                filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

            if (stream.Length <= offset) return ([], offset);
            stream.Seek(offset, SeekOrigin.Begin);

            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true,
                bufferSize: 4096, leaveOpen: true);

            var entries = new List<LogEntry>();
            string? line;
            while ((line = await reader.ReadLineAsync()) is not null)
            {
                var entry = ParseLine(line);
                if (entry is not null) entries.Add(entry);
            }

            return (entries, stream.Position);
        }
        catch { return ([], offset); }
    }

    public async Task<IReadOnlyList<LogEntry>> ReadAllAsync(string filePath)
    {
        var (entries, _) = await ReadFromOffsetAsync(filePath, 0);
        return entries;
    }

    // #60 – Parse a single log line
    public LogEntry? ParseLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;

        // Try UE5 format
        var m = Ue5Pattern.Match(line);
        if (m.Success)
        {
            var ts = ParseUe5Timestamp(m.Groups[1].Value);
            var severity = MapUe5Verbosity(m.Groups[3].Value);
            return new LogEntry
            {
                Timestamp = ts,
                Severity  = severity,
                Source    = m.Groups[2].Value,
                Message   = m.Groups[4].Value.Trim(),
                RawLine   = line,
            };
        }

        // Try simple format
        m = SimplePattern.Match(line);
        if (m.Success)
        {
            var ts = ParseSimpleTimestamp(m.Groups[1].Value);
            var severity = MapSimpleSeverity(m.Groups[2].Value);
            return new LogEntry
            {
                Timestamp = ts,
                Severity  = severity,
                Source    = string.Empty,
                Message   = m.Groups[3].Value.Trim(),
                RawLine   = line,
            };
        }

        // Raw fallback
        return new LogEntry
        {
            Timestamp = default,
            Severity  = LogSeverity.Unknown,
            Source    = string.Empty,
            Message   = line,
            RawLine   = line,
        };
    }

    private static DateTime ParseUe5Timestamp(string raw)
    {
        // 2024.01.15-12.30.45:123
        if (DateTime.TryParseExact(raw[..19], "yyyy.MM.dd-HH.mm.ss",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var dt))
            return dt;
        return default;
    }

    private static DateTime ParseSimpleTimestamp(string raw)
    {
        if (TimeSpan.TryParse(raw, out var ts))
            return DateTime.Today.Add(ts);
        return default;
    }

    private static LogSeverity MapUe5Verbosity(string v) => v switch
    {
        "Error" or "Fatal"               => LogSeverity.Error,
        "Warning"                        => LogSeverity.Warning,
        "Display" or ""                  => LogSeverity.Info,
        "Verbose" or "VeryVerbose"       => LogSeverity.Debug,
        _                                => LogSeverity.Info,
    };

    private static LogSeverity MapSimpleSeverity(string v) => v.ToUpperInvariant() switch
    {
        "ERROR"                          => LogSeverity.Error,
        "WARN" or "WARNING"              => LogSeverity.Warning,
        "INFO"                           => LogSeverity.Info,
        "DEBUG" or "TRACE"               => LogSeverity.Debug,
        _                                => LogSeverity.Unknown,
    };
}
