using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using SietchConsole.Core.Interfaces;
using SietchConsole.Core.Models;
using System.IO;

namespace Sietch_Console.ViewModels;

public partial class LogsViewModel : ObservableObject
{
    private readonly ILogFileService    _logService;
    private readonly ILogAnalysisService _analysisService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DispatcherTimer    _tailTimer;

    private BattlegroupProfile? _profile;
    private readonly List<LogEntry> _allEntries = [];
    private long _logFileOffset;
    private const int MaxEntries = 5_000;

    // ── Status ────────────────────────────────────────────────────────
    [ObservableProperty] private bool   _isStreaming;
    [ObservableProperty] private bool   _autoScroll = true;
    [ObservableProperty] private string? _activeLogFile;
    [ObservableProperty] private IReadOnlyList<string> _availableLogFiles = [];

    // ── Entries ───────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEntries))]
    private ObservableCollection<LogEntry> _filteredEntries = [];

    public bool HasEntries => FilteredEntries.Count > 0;

    // ── Issues (#71, #72, #73) ────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDetectedIssues))]
    [NotifyPropertyChangedFor(nameof(IssueCountLabel))]
    private ObservableCollection<DetectedIssue> _detectedIssues = [];

    public bool HasDetectedIssues => DetectedIssues.Count > 0;
    public string IssueCountLabel => DetectedIssues.Count == 0
        ? "No issues detected"
        : $"{DetectedIssues.Count} issue{(DetectedIssues.Count == 1 ? "" : "s")} detected";

    // ── Filtering (#70) ───────────────────────────────────────────────
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private LogSeverity? _severityFilter; // null = All

    partial void OnSearchTextChanged(string value)    => ApplyFilter();
    partial void OnSeverityFilterChanged(LogSeverity? value) => ApplyFilter();

    public LogsViewModel(
        ILogFileService logService,
        ILogAnalysisService analysisService,
        IServiceScopeFactory scopeFactory)
    {
        _logService      = logService;
        _analysisService = analysisService;
        _scopeFactory    = scopeFactory;

        _tailTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _tailTimer.Tick += async (_, _) => await PollLogFileAsync();
        _tailTimer.Start();
    }

    public async Task InitializeAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var settingsRepo = scope.ServiceProvider.GetRequiredService<IApplicationSettingsRepository>();
        var profileRepo  = scope.ServiceProvider.GetRequiredService<IBattlegroupProfileRepository>();

        var settings = await settingsRepo.GetAsync();
        if (int.TryParse(settings.LastOpenedBattlegroupId, out var id))
            _profile = await profileRepo.GetByIdAsync(id);
        else
            _profile = (await profileRepo.GetAllAsync()).FirstOrDefault();

        if (_profile is not null)
        {
            AvailableLogFiles = _logService.GetLogFiles(_profile);
            if (AvailableLogFiles.Count > 0)
            {
                ActiveLogFile   = AvailableLogFiles[0];
                _logFileOffset  = 0;
                IsStreaming     = true;
            }
        }
    }

    // ── Log file selection ────────────────────────────────────────────
    [RelayCommand]
    private async Task SelectLogFileAsync(string filePath)
    {
        ActiveLogFile  = filePath;
        _logFileOffset = 0;
        _allEntries.Clear();
        FilteredEntries = [];
        DetectedIssues  = [];

        // Load existing content once, then tail for new entries
        var all = await _logService.ReadAllAsync(filePath);
        _logFileOffset = 0;
        // Fast-forward offset without re-adding entries; let tail pick up from end
        var (_, newOffset) = await _logService.ReadFromOffsetAsync(filePath, 0);
        _logFileOffset = newOffset;

        AddEntries(all);
        IsStreaming = true;
    }

    // ── Live tail (#69) ───────────────────────────────────────────────
    private async Task PollLogFileAsync()
    {
        if (ActiveLogFile is null || !IsStreaming) return;

        var (entries, newOffset) = await _logService.ReadFromOffsetAsync(ActiveLogFile, _logFileOffset);
        if (entries.Count == 0) return;

        _logFileOffset = newOffset;
        AddEntries(entries);
    }

    private void AddEntries(IReadOnlyList<LogEntry> entries)
    {
        foreach (var entry in entries)
        {
            _allEntries.Add(entry);
            if (MatchesFilter(entry))
                FilteredEntries.Add(entry);
        }

        // Cap collection
        while (_allEntries.Count > MaxEntries)
        {
            var removed = _allEntries[0];
            _allEntries.RemoveAt(0);
            if (FilteredEntries.Count > 0 && FilteredEntries[0] == removed)
                FilteredEntries.RemoveAt(0);
        }

        OnPropertyChanged(nameof(HasEntries));
        RefreshIssues();
    }

    // ── Filtering (#70) ───────────────────────────────────────────────
    private void ApplyFilter()
    {
        FilteredEntries = new ObservableCollection<LogEntry>(_allEntries.Where(MatchesFilter));
        OnPropertyChanged(nameof(HasEntries));
    }

    private bool MatchesFilter(LogEntry entry)
    {
        if (SeverityFilter is not null && entry.Severity != SeverityFilter.Value) return false;
        if (!string.IsNullOrWhiteSpace(SearchText) &&
            !entry.Message.Contains(SearchText, StringComparison.OrdinalIgnoreCase) &&
            !entry.Source.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
            return false;
        return true;
    }

    // ── Severity filter commands ──────────────────────────────────────
    [RelayCommand] private void FilterAll()     => SeverityFilter = null;
    [RelayCommand] private void FilterErrors()  => SeverityFilter = LogSeverity.Error;
    [RelayCommand] private void FilterWarnings()=> SeverityFilter = LogSeverity.Warning;
    [RelayCommand] private void FilterInfo()    => SeverityFilter = LogSeverity.Info;

    [RelayCommand] private void ClearLog()
    {
        _allEntries.Clear();
        FilteredEntries = [];
        DetectedIssues  = [];
        _logFileOffset  = 0;
        OnPropertyChanged(nameof(HasEntries));
    }

    // ── Issue detection (#71) ─────────────────────────────────────────
    private void RefreshIssues()
    {
        var issues = _analysisService.Analyze(_allEntries);
        DetectedIssues = new ObservableCollection<DetectedIssue>(issues);
        OnPropertyChanged(nameof(HasDetectedIssues));
        OnPropertyChanged(nameof(IssueCountLabel));
    }

    // ── Copy diagnostics report (#74) ────────────────────────────────
    [RelayCommand]
    private void CopyReport()
    {
        var report = _analysisService.GenerateReport(_profile, ActiveLogFile, _allEntries, DetectedIssues);
        Clipboard.SetText(report);
    }

    // ── Export logs (#75) ─────────────────────────────────────────────
    [RelayCommand]
    private void ExportLogs()
    {
        var dialog = new SaveFileDialog
        {
            Title            = "Export Logs",
            Filter           = "Log files (*.log)|*.log|Text files (*.txt)|*.txt|All files (*.*)|*.*",
            DefaultExt       = ".log",
            FileName         = $"battlegroup_{DateTime.Now:yyyyMMdd_HHmmss}.log",
        };

        if (dialog.ShowDialog() != true) return;

        var report = _analysisService.GenerateReport(_profile, ActiveLogFile, _allEntries, DetectedIssues);
        File.WriteAllText(dialog.FileName, report);
    }
}
