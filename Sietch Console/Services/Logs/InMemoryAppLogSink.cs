using System.Collections.Concurrent;
using System.Windows;
using Microsoft.Extensions.Logging;
using SietchConsole.Core.Interfaces;
using SietchConsole.Core.Models;

namespace Sietch_Console.Services.Logs;

/// <summary>
/// Dual-purpose singleton that acts as a <see cref="ILoggerProvider"/> (so it receives
/// all Microsoft.Extensions.Logging events) and as an <see cref="IAppLogSink"/> (so the
/// AppLogsViewModel can read and display those events in-app, #143).
///
/// Entries are buffered in a ring-buffer capped at <see cref="MaxEntries"/>, newest-first.
/// </summary>
public sealed class InMemoryAppLogSink : IAppLogSink, ILoggerProvider
{
    private const int MaxEntries = 500;

    private readonly ConcurrentQueue<AppLogEntry> _queue = new();

    public IReadOnlyList<AppLogEntry> Entries
    {
        get
        {
            // Return a snapshot, newest first
            var snap = _queue.ToArray();
            Array.Reverse(snap);
            return snap;
        }
    }

    public event EventHandler? EntriesChanged;

    public void Clear()
    {
        while (_queue.TryDequeue(out _)) { }
        RaiseEntriesChanged();
    }

    // ── ILoggerProvider ───────────────────────────────────────────────────────

    public ILogger CreateLogger(string categoryName) => new InMemoryLogger(this, categoryName);

    public void Dispose() { }

    // ── Internal ──────────────────────────────────────────────────────────────

    internal void AddEntry(AppLogEntry entry)
    {
        _queue.Enqueue(entry);

        // Trim oldest entries once we exceed the cap
        while (_queue.Count > MaxEntries)
            _queue.TryDequeue(out _);

        RaiseEntriesChanged();
    }

    private void RaiseEntriesChanged()
    {
        if (Application.Current is null) return;
        Application.Current.Dispatcher.InvokeAsync(
            () => EntriesChanged?.Invoke(this, EventArgs.Empty));
    }

    // ── ILogger implementation ────────────────────────────────────────────────

    private sealed class InMemoryLogger(InMemoryAppLogSink sink, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) =>
            logLevel >= LogLevel.Information; // skip Trace/Debug to keep the log clean

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            var entry = new AppLogEntry(
                Timestamp : DateTime.Now,
                Level     : logLevel.ToString(),
                Category  : ShortenCategory(category),
                Message   : formatter(state, exception),
                Exception : exception?.ToString());

            sink.AddEntry(entry);
        }

        private static string ShortenCategory(string cat)
        {
            // Keep only the last segment, e.g. "Sietch_Console.Services.Control.BattlegroupControlService"
            // → "BattlegroupControlService"
            var dot = cat.LastIndexOf('.');
            return dot >= 0 ? cat[(dot + 1)..] : cat;
        }
    }
}
