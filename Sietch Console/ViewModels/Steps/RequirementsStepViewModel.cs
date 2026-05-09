using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SietchConsole.Core.Interfaces;
using SietchConsole.Core.Models;
using System.Collections.ObjectModel;

namespace Sietch_Console.ViewModels.Steps;

public partial class RequirementsStepViewModel : ObservableObject
{
    private readonly ISystemReadinessService _readinessService;

    public string Title => "Requirements";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanProceed))]
    [NotifyPropertyChangedFor(nameof(HasResults))]
    private ObservableCollection<DiagnosticsResult> _results = [];

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private bool _proceedDespiteFailures;

    public bool HasResults => Results.Count > 0;
    public bool HasFailures => Results.Any(r => r.Severity == "Failure");
    public bool CanProceed => HasResults && (!HasFailures || ProceedDespiteFailures);

    public RequirementsStepViewModel(ISystemReadinessService readinessService)
    {
        _readinessService = readinessService;
    }

    [RelayCommand]
    public async Task RunChecksAsync()
    {
        IsRunning = true;
        Results.Clear();
        try
        {
            var results = await _readinessService.RunAllChecksAsync();
            foreach (var r in results)
                Results.Add(r);
        }
        finally
        {
            IsRunning = false;
            OnPropertyChanged(nameof(HasFailures));
            OnPropertyChanged(nameof(CanProceed));
        }
    }

    partial void OnProceedDespiteFailuresChanged(bool value)
        => OnPropertyChanged(nameof(CanProceed));
}
