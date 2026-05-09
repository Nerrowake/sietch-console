using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SietchConsole.Core.Interfaces;
using SietchConsole.Core.Models;
using System.Collections.ObjectModel;

namespace Sietch_Console.ViewModels;

public partial class DiagnosticsViewModel : ObservableObject
{
    private readonly ISystemReadinessService _readinessService;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasResults))]
    private ObservableCollection<DiagnosticsResult> _results = [];

    public bool HasResults => Results.Count > 0;

    public int PassCount => Results.Count(r => r.Severity == "Pass");
    public int WarningCount => Results.Count(r => r.Severity == "Warning");
    public int FailureCount => Results.Count(r => r.Severity == "Failure");

    public DiagnosticsViewModel(ISystemReadinessService readinessService)
    {
        _readinessService = readinessService;
    }

    [RelayCommand(CanExecute = nameof(CanRunChecks))]
    private async Task RunChecksAsync()
    {
        IsRunning = true;
        Results.Clear();
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(PassCount));
        OnPropertyChanged(nameof(WarningCount));
        OnPropertyChanged(nameof(FailureCount));

        try
        {
            var results = await _readinessService.RunAllChecksAsync();
            foreach (var r in results)
                Results.Add(r);
        }
        finally
        {
            IsRunning = false;
            OnPropertyChanged(nameof(HasResults));
            OnPropertyChanged(nameof(PassCount));
            OnPropertyChanged(nameof(WarningCount));
            OnPropertyChanged(nameof(FailureCount));
        }
    }

    private bool CanRunChecks() => !IsRunning;
}
