namespace SietchConsole.Core.Models;

public class DetectedIssue
{
    public string                  Title           { get; init; } = string.Empty;
    public string                  Explanation     { get; init; } = string.Empty;
    public IReadOnlyList<string>   Recommendations { get; init; } = [];
    public LogSeverity             Severity        { get; init; } = LogSeverity.Error;
    public LogEntry?               TriggerEntry    { get; init; }
}
