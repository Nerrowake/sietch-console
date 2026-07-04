using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows;

namespace Sietch_Console.ViewModels.Steps;

public partial class ProgressStepViewModel : ObservableObject
{
    public string Title => "Installing";

    private CancellationTokenSource? _cts;
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(30);

    [ObservableProperty] private double _progressPercent;
    [ObservableProperty] private string _currentOperation = "Waiting to start…";
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    private bool   _isRunning;
    [ObservableProperty] private bool   _isComplete;
    [ObservableProperty] private bool   _hasFailed;

    // #186 – timeout + cancel
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanProceed))]
    private bool _isCancelled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanProceed))]
    private bool _isTimedOut;

    public ObservableCollection<string> LogLines { get; } = [];

    public bool CanProceed => IsComplete && !HasFailed && !IsCancelled && !IsTimedOut;

    // #186 — Returns a token that cancels after 30 minutes; store to allow manual cancel
    public CancellationToken Begin()
    {
        _cts           = new CancellationTokenSource(Timeout);
        IsRunning      = true;
        IsCancelled    = false;
        IsTimedOut     = false;
        HasFailed      = false;
        IsComplete     = false;
        ProgressPercent = 0;
        LogLines.Clear();
        return _cts.Token;
    }

    [RelayCommand(CanExecute = nameof(IsRunning))]
    private void Cancel()
    {
        if (_cts is null || !IsRunning) return;
        _cts.Cancel();
        IsCancelled     = true;
        IsRunning       = false;
        CurrentOperation = "Installation cancelled.";
        AppendLog("— Cancelled by user —");
    }

    public void AppendLog(string line)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            _ = dispatcher.InvokeAsync(() => AppendLog(line));
            return;
        }

        LogLines.Add(line);
        OnPropertyChanged(nameof(CanProceed));
    }

    public void MarkTimedOut()
    {
        IsTimedOut      = true;
        IsRunning       = false;
        CurrentOperation = "Installation timed out after 30 minutes.";
        AppendLog("— Timed out —");
        OnPropertyChanged(nameof(CanProceed));
    }

    partial void OnIsCompleteChanged(bool value)
    {
        IsRunning = false;
        OnPropertyChanged(nameof(CanProceed));
    }

    partial void OnHasFailedChanged(bool value) => OnPropertyChanged(nameof(CanProceed));
}
