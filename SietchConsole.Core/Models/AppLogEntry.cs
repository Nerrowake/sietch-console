namespace SietchConsole.Core.Models;

/// <summary>A single in-memory application log entry captured from the .NET logging pipeline (#143).</summary>
public sealed record AppLogEntry(
    DateTime    Timestamp,
    string      Level,
    string      Category,
    string      Message,
    string?     Exception = null);
