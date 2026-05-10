using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SietchConsole.Core.Interfaces;
using SietchConsole.Core.Models;

namespace Sietch_Console.ViewModels;

/// <summary>
/// Shows the in-app Microsoft.Extensions.Logging stream so users can
/// diagnose Sietch Console itself without inspecting external log files (#143).
/// </summary>
public partial class AppLogsViewModel : ObservableObject
{
    private readonly IAppLogSink _sink;

    // ── State ─────────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEntries))]
    private ObservableCollection<AppLogEntry> _entries = [];

    public bool HasEntries => Entries.Count > 0;

    // ── Filtering ─────────────────────────────────────────────────────
    [ObservableProperty] private string  _searchText  = string.Empty;
    [ObservableProperty] private string? _levelFilter;            // null = All

    /// <summary>Levels available in the filter ComboBox.</summary>
    public IReadOnlyList<string?> LevelChoices { get; } =
        [null, "Information", "Warning", "Error", "Critical"];

    partial void OnSearchTextChanged(string value)    => ApplyFilter();
    partial void OnLevelFilterChanged(string? value)  => ApplyFilter();

    // ── Constructor ───────────────────────────────────────────────────

    public AppLogsViewModel(IAppLogSink sink)
    {
        _sink = sink;
        _sink.EntriesChanged += (_, _) => ApplyFilter();
        ApplyFilter();
    }

    // ── Filter ────────────────────────────────────────────────────────

    private void ApplyFilter()
    {
        var raw = _sink.Entries;   // snapshot; newest-first from the sink

        IEnumerable<AppLogEntry> filtered = raw;

        if (!string.IsNullOrWhiteSpace(LevelFilter))
            filtered = filtered.Where(e => e.Level.Equals(LevelFilter, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(SearchText))
            filtered = filtered.Where(e =>
                e.Message.Contains(SearchText,  StringComparison.OrdinalIgnoreCase) ||
                e.Category.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

        Entries = new ObservableCollection<AppLogEntry>(filtered);
        OnPropertyChanged(nameof(HasEntries));
    }

    // ── Commands ──────────────────────────────────────────────────────

    [RelayCommand]
    private void ClearLog()
    {
        _sink.Clear();
        Entries = [];
        OnPropertyChanged(nameof(HasEntries));
    }

    [RelayCommand]
    private void Refresh() => ApplyFilter();
}
