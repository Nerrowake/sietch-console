using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using SietchConsole.Core.Interfaces;
using SietchConsole.Core.Models;

namespace Sietch_Console.ViewModels;

/// <summary>
/// Backs the Metrics view — server health charts and downtime log (#163, #164).
/// </summary>
public partial class MetricsViewModel : ObservableObject
{
    // IMetricsRepository is scoped; this ViewModel is a singleton.
    // We open a short-lived scope per async operation instead of injecting the repo directly.
    private readonly IServiceScopeFactory    _scopeFactory;
    private readonly IMetricsCollectorService _collector;
    private readonly IActiveProfileService    _activeProfile;

    // ── Time range selector ───────────────────────────────────────────────────

    public ObservableCollection<string> TimeRangeOptions { get; } =
        new(["Last 1 hour", "Last 6 hours", "Last 24 hours", "Last 7 days", "Last 30 days"]);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TimeRangeHours))]
    private int _selectedTimeRangeIndex = 2; // Default: last 24 h

    public double TimeRangeHours => SelectedTimeRangeIndex switch
    {
        0 =>     1,
        1 =>     6,
        2 =>    24,
        3 =>   168,
        4 =>   720,
        _ =>    24,
    };

    // ── Charts ────────────────────────────────────────────────────────────────

    public PlotModel PlayerCountModel { get; } = CreatePlotModel("Players");
    public PlotModel CpuModel         { get; } = CreatePlotModel("CPU %");
    public PlotModel MemoryModel      { get; } = CreatePlotModel("Memory (MB)");

    // ── Uptime summary ────────────────────────────────────────────────────────

    [ObservableProperty] private string _uptimeSummary    = "—";
    [ObservableProperty] private string _availabilityPct  = "—";
    [ObservableProperty] private ObservableCollection<DowntimeEvent> _downtimeEvents = [];
    public bool HasDowntimeEvents => DowntimeEvents.Count > 0;

    // ── Shared ────────────────────────────────────────────────────────────────

    [ObservableProperty] private bool   _isLoading;
    [ObservableProperty] private bool   _isCollecting;
    [ObservableProperty] private string _statusMessage = string.Empty;

    // ── Constructor ───────────────────────────────────────────────────────────

    public MetricsViewModel(
        IServiceScopeFactory     scopeFactory,
        IMetricsCollectorService collector,
        IActiveProfileService    activeProfile)
    {
        _scopeFactory  = scopeFactory;
        _collector     = collector;
        _activeProfile = activeProfile;

        _collector.SnapshotCollected += OnSnapshotCollected;
        IsCollecting = _collector.IsCollecting;

        _ = LoadChartsAsync();
    }

    // ── Load ──────────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task LoadChartsAsync()
    {
        var profile = _activeProfile.Current;
        if (profile is null) return;

        IsLoading     = true;
        StatusMessage = string.Empty;
        try
        {
            var to        = DateTime.UtcNow;
            var from      = to.AddHours(-TimeRangeHours);
            var profileId = profile.Id.ToString();

            IReadOnlyList<ServerMetricSnapshot> snapshots;
            IReadOnlyList<DowntimeEvent>        events;

            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var repo = scope.ServiceProvider.GetRequiredService<IMetricsRepository>();
                snapshots = await repo.GetSnapshotsAsync(profileId, from, to);
                events    = await repo.GetDowntimeEventsAsync(profileId, from, to);
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                PopulateChart(PlayerCountModel, snapshots, s => s.PlayerCount);
                PopulateChart(CpuModel,         snapshots, s => s.CpuPercent);
                PopulateChart(MemoryModel,       snapshots, s => s.MemoryMb);

                DowntimeEvents.Clear();
                foreach (var e in events)
                    DowntimeEvents.Add(e);

                OnPropertyChanged(nameof(HasDowntimeEvents));
                UpdateUptimeSummary(snapshots, events, from, to);
            });
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load metrics: {ex.Message}";
        }
        finally { IsLoading = false; }
    }

    partial void OnSelectedTimeRangeIndexChanged(int value)
        => _ = LoadChartsAsync();

    // ── Live snapshot handler ─────────────────────────────────────────────────

    private void OnSnapshotCollected(object? sender, ServerMetricSnapshot snapshot)
    {
        IsCollecting = _collector.IsCollecting;

        // Only append if this snapshot falls within the current time range.
        var from = DateTime.UtcNow.AddHours(-TimeRangeHours);
        if (snapshot.Timestamp < from) return;

        Application.Current.Dispatcher.Invoke(() =>
        {
            AppendDataPoint(PlayerCountModel, snapshot.Timestamp, snapshot.PlayerCount);
            AppendDataPoint(CpuModel,         snapshot.Timestamp, snapshot.CpuPercent);
            AppendDataPoint(MemoryModel,       snapshot.Timestamp, snapshot.MemoryMb);
        });
    }

    // ── Chart helpers ─────────────────────────────────────────────────────────

    private static PlotModel CreatePlotModel(string yLabel)
    {
        var model = new PlotModel
        {
            Background  = OxyColor.FromArgb(0, 0, 0, 0),  // transparent
            TextColor   = OxyColor.FromRgb(0xF3, 0xEB, 0xDD),
            PlotAreaBorderColor = OxyColor.FromRgb(0x2A, 0x33, 0x3F),
        };

        model.Axes.Add(new DateTimeAxis
        {
            Position        = AxisPosition.Bottom,
            StringFormat    = "HH:mm",
            AxislineColor   = OxyColor.FromRgb(0x7B, 0x6F, 0x5D),
            TicklineColor   = OxyColor.FromRgb(0x7B, 0x6F, 0x5D),
            TextColor       = OxyColor.FromRgb(0xB8, 0xAA, 0x92),
            MajorGridlineStyle = LineStyle.Dot,
            MajorGridlineColor = OxyColor.FromRgb(0x2A, 0x33, 0x3F),
        });

        model.Axes.Add(new LinearAxis
        {
            Position        = AxisPosition.Left,
            Title           = yLabel,
            TitleColor      = OxyColor.FromRgb(0xB8, 0xAA, 0x92),
            AxislineColor   = OxyColor.FromRgb(0x7B, 0x6F, 0x5D),
            TicklineColor   = OxyColor.FromRgb(0x7B, 0x6F, 0x5D),
            TextColor       = OxyColor.FromRgb(0xB8, 0xAA, 0x92),
            MajorGridlineStyle = LineStyle.Dot,
            MajorGridlineColor = OxyColor.FromRgb(0x2A, 0x33, 0x3F),
            Minimum         = 0,
        });

        model.Series.Add(new LineSeries
        {
            Color           = OxyColor.FromRgb(0xC4, 0x6A, 0x2B),  // AccentPrimary
            StrokeThickness = 1.5,
            MarkerType      = MarkerType.None,
        });

        return model;
    }

    private static void PopulateChart(
        PlotModel model,
        IReadOnlyList<ServerMetricSnapshot> snapshots,
        Func<ServerMetricSnapshot, double> selector)
    {
        var series = (LineSeries)model.Series[0];
        series.Points.Clear();

        foreach (var s in snapshots)
            series.Points.Add(new DataPoint(DateTimeAxis.ToDouble(s.Timestamp), selector(s)));

        model.InvalidatePlot(true);
    }

    private static void AppendDataPoint(PlotModel model, DateTime timestamp, double value)
    {
        var series = (LineSeries)model.Series[0];
        series.Points.Add(new DataPoint(DateTimeAxis.ToDouble(timestamp), value));
        model.InvalidatePlot(true);
    }

    private void UpdateUptimeSummary(
        IReadOnlyList<ServerMetricSnapshot> snapshots,
        IReadOnlyList<DowntimeEvent>        events,
        DateTime from, DateTime to)
    {
        if (snapshots.Count == 0)
        {
            UptimeSummary   = "No data";
            AvailabilityPct = "—";
            return;
        }

        var windowMinutes  = (to - from).TotalMinutes;
        var downtimeMinutes = events
            .Where(e => !e.IsOpen)
            .Sum(e => e.Duration!.Value.TotalMinutes);

        var uptimeMinutes = Math.Max(0, windowMinutes - downtimeMinutes);
        var pct           = windowMinutes > 0 ? 100.0 * uptimeMinutes / windowMinutes : 0;

        UptimeSummary   = $"{uptimeMinutes:0} min up / {downtimeMinutes:0} min down";
        AvailabilityPct = $"{pct:0.0}%";
    }
}
