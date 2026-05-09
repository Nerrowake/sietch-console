namespace SietchConsole.Core.Models;

public class LogEntry
{
    public DateTime  Timestamp { get; init; }
    public LogSeverity Severity { get; init; }
    public string    Source    { get; init; } = string.Empty;
    public string    Message   { get; init; } = string.Empty;
    public string    RawLine   { get; init; } = string.Empty;

    public string SeverityLabel => Severity switch
    {
        LogSeverity.Error   => "ERROR",
        LogSeverity.Warning => "WARN ",
        LogSeverity.Info    => "INFO ",
        LogSeverity.Debug   => "DEBUG",
        _                   => "     ",
    };

    public string TimestampLabel => Timestamp == default
        ? "          "
        : Timestamp.ToString("HH:mm:ss");
}
