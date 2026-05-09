using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace Sietch_Console.ViewModels.Steps;

public partial class ProgressStepViewModel : ObservableObject
{
    public string Title => "Installing";

    [ObservableProperty]
    private double _progressPercent;

    [ObservableProperty]
    private string _currentOperation = "Waiting to start…";

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private bool _isComplete;

    [ObservableProperty]
    private bool _hasFailed;

    public ObservableCollection<string> LogLines { get; } = [];

    public bool CanProceed => IsComplete && !HasFailed;

    public void AppendLog(string line)
    {
        LogLines.Add(line);
        OnPropertyChanged(nameof(CanProceed));
    }

    partial void OnIsCompleteChanged(bool value)
        => OnPropertyChanged(nameof(CanProceed));
}
