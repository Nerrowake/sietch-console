using SietchConsole.Core.Models;

namespace SietchConsole.Core.Interfaces;

/// <summary>
/// In-memory sink that collects structured application log entries
/// so they can be displayed in the App Logs view (#143).
/// </summary>
public interface IAppLogSink
{
    /// <summary>A snapshot of all buffered entries, newest-first.</summary>
    IReadOnlyList<AppLogEntry> Entries { get; }

    /// <summary>Raised (on the UI thread) whenever new entries are added.</summary>
    event EventHandler? EntriesChanged;

    /// <summary>Remove all buffered entries.</summary>
    void Clear();
}
