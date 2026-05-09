using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SietchConsole.Core.Interfaces;
using SietchConsole.Core.Models;

namespace Sietch_Console.Services.Logs;

public class LogAnalysisService : ILogAnalysisService
{
    // #71 – Error detection rules
    private sealed record ErrorRule(
        string Pattern,
        string Title,
        string Explanation,
        string[] Recommendations,
        LogSeverity Severity = LogSeverity.Error);

    private static readonly ErrorRule[] Rules =
    [
        new(
            Pattern:         @"(?i)port.*already in use|address already in use|bind.*failed|WSAEADDRINUSE",
            Title:           "Port Already In Use",
            Explanation:     "The game port is already bound by another process. The server cannot start until the port is free.",
            Recommendations: ["Stop any other running server instances.", "Change the game port in Settings.", "Reboot the host to clear lingering port bindings."],
            Severity:        LogSeverity.Error
        ),
        new(
            Pattern:         @"(?i)failed to authenticate|not authenticated|steam.*auth.*fail|invalid steam",
            Title:           "Steam Authentication Failed",
            Explanation:     "The server could not authenticate with Steam. Players may be unable to connect.",
            Recommendations: ["Verify your Steam credentials and session token.", "Ensure SteamCMD is up to date.", "Run the setup wizard again to re-enter the Dune account token."]
        ),
        new(
            Pattern:         @"(?i)out of memory|insufficient memory|memory.*allocation.*fail|bad_alloc",
            Title:           "Out of Memory",
            Explanation:     "The server ran out of available RAM. This causes crashes and connection drops.",
            Recommendations: ["Reduce MaxPlayers in Settings.", "Close other applications on the host.", "Increase VM RAM if running in Hyper-V."]
        ),
        new(
            Pattern:         @"(?i)connection refused|connection.*reset|ECONNREFUSED|peer.*closed",
            Title:           "Connection Refused",
            Explanation:     "Incoming connections are being rejected, likely due to a firewall rule or the server not fully started.",
            Recommendations: ["Check Windows Firewall allows ports 7777–7810 UDP.", "Verify the server has finished starting up.", "Run Diagnostics to check firewall status."]
        ),
        new(
            Pattern:         @"(?i)failed to load.*map|map.*not found|could not find.*level",
            Title:           "Map Not Found",
            Explanation:     "The server cannot load the game map. This is usually a missing or corrupted game file.",
            Recommendations: ["Verify the game installation is complete.", "Re-run the setup wizard to repair the server package.", "Check the install path in Settings."]
        ),
        new(
            Pattern:         @"(?i)network adapter.*not found|no.*network.*interface|adapter.*unavailable",
            Title:           "Network Adapter Unavailable",
            Explanation:     "The configured network adapter is missing or disabled.",
            Recommendations: ["Run Diagnostics to verify network adapter status.", "Check the adapter setting in the setup wizard.", "Ensure the Hyper-V virtual switch is configured correctly."]
        ),
        new(
            Pattern:         @"(?i)disk.*full|no space left|ENOSPC|not enough.*space",
            Title:           "Disk Full",
            Explanation:     "The host drive is out of free space. Log rotation and saves will fail.",
            Recommendations: ["Free up disk space on the install drive.", "Clear old backups from the Backups section.", "Move the install to a larger drive."],
            Severity:        LogSeverity.Warning
        ),
        new(
            Pattern:         @"(?i)failed to open.*log|cannot write.*log|log.*permission denied",
            Title:           "Log Write Failure",
            Explanation:     "The server cannot write to its log file. Output will be incomplete.",
            Recommendations: ["Check file permissions on the install directory.", "Ensure another process is not locking the log file.", "Run Sietch Console as Administrator."],
            Severity:        LogSeverity.Warning
        ),
        new(
            Pattern:         @"(?i)cluster.*timeout|cluster.*unreachable|cluster.*not ready",
            Title:           "Cluster Not Ready",
            Explanation:     "The battlegroup cluster is taking too long to become ready, or a node is unreachable.",
            Recommendations: ["Wait 60 seconds and check status again.", "Restart the battlegroup if startup takes more than 5 minutes.", "Check VM network configuration."]
        ),
        new(
            Pattern:         @"(?i)certificate.*invalid|SSL.*error|TLS.*handshake.*fail",
            Title:           "SSL/TLS Error",
            Explanation:     "A secure connection failed. This may block remote management or player authentication.",
            Recommendations: ["Ensure system time is accurate.", "Check if antivirus is intercepting HTTPS traffic.", "Reinstall the server package if certificates are corrupt."],
            Severity:        LogSeverity.Warning
        ),
    ];

    // #71 / #72 – Analyze entries and return detected issues with friendly translations
    public IReadOnlyList<DetectedIssue> Analyze(IEnumerable<LogEntry> entries)
    {
        var seen = new HashSet<string>();
        var issues = new List<DetectedIssue>();

        foreach (var entry in entries)
        {
            if (entry.Severity is not (LogSeverity.Error or LogSeverity.Warning)) continue;

            foreach (var rule in Rules)
            {
                if (seen.Contains(rule.Title)) continue;
                if (!Regex.IsMatch(entry.RawLine, rule.Pattern)) continue;

                seen.Add(rule.Title);
                issues.Add(new DetectedIssue
                {
                    Title           = rule.Title,
                    Explanation     = rule.Explanation,
                    Recommendations = rule.Recommendations,
                    Severity        = rule.Severity,
                    TriggerEntry    = entry,
                });
            }
        }

        return issues;
    }

    // #74 – Generate plain-text diagnostics report
    public string GenerateReport(
        BattlegroupProfile? profile,
        string? logFile,
        IEnumerable<LogEntry> recentEntries,
        IEnumerable<DetectedIssue> issues)
    {
        var sb = new StringBuilder();
        var issueList = issues.ToList();
        var entryList = recentEntries.TakeLast(50).ToList();

        sb.AppendLine("=== Sietch Console Diagnostics Report ===");
        sb.AppendLine($"Generated : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Profile   : {profile?.Name ?? "None"}");
        sb.AppendLine($"Active Log: {logFile ?? "None"}");
        sb.AppendLine();

        sb.AppendLine($"DETECTED ISSUES ({issueList.Count})");
        sb.AppendLine(new string('─', 40));
        if (issueList.Count == 0)
        {
            sb.AppendLine("No issues detected.");
        }
        else
        {
            foreach (var issue in issueList)
            {
                sb.AppendLine($"[{issue.Severity.ToString().ToUpperInvariant()}] {issue.Title}");
                sb.AppendLine($"  {issue.Explanation}");
                sb.AppendLine("  Recommendations:");
                foreach (var rec in issue.Recommendations)
                    sb.AppendLine($"    • {rec}");
                if (issue.TriggerEntry is not null)
                    sb.AppendLine($"  Trigger: {issue.TriggerEntry.RawLine}");
                sb.AppendLine();
            }
        }

        sb.AppendLine($"RECENT LOG (last {entryList.Count} lines)");
        sb.AppendLine(new string('─', 40));
        if (entryList.Count == 0)
        {
            sb.AppendLine("No log entries available.");
        }
        else
        {
            foreach (var entry in entryList)
                sb.AppendLine($"[{entry.TimestampLabel}] {entry.SeverityLabel}  {entry.Message}");
        }

        return sb.ToString();
    }
}
