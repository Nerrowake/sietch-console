using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Sietch_Console.Services.Installation;
using Sietch_Console.ViewModels.Steps;
using SietchConsole.Core.Interfaces;
using SietchConsole.Core.Models;

namespace Sietch_Console.ViewModels;

public record WizardStepIndicator(string Title, bool IsActive, bool IsCompleted);

public partial class SetupWizardViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly InstallationOrchestrator _orchestrator;
    private readonly List<ObservableObject> _steps;
    private SetupWizardState _state = new();
    private CancellationTokenSource? _installCts;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentStep))]
    [NotifyPropertyChangedFor(nameof(StepIndicators))]
    [NotifyPropertyChangedFor(nameof(StepLabel))]
    [NotifyPropertyChangedFor(nameof(CanGoBack))]
    [NotifyPropertyChangedFor(nameof(IsLastStep))]
    [NotifyPropertyChangedFor(nameof(NextLabel))]
    private int _currentStepIndex;

    [ObservableProperty]
    private bool _hasResumeOffer;

    public ObservableObject CurrentStep => _steps[CurrentStepIndex];

    public IReadOnlyList<WizardStepIndicator> StepIndicators =>
        _steps.Select((s, i) => new WizardStepIndicator(
            StepTitle(s),
            i == CurrentStepIndex,
            i < CurrentStepIndex)).ToList();

    public string StepLabel => $"Step {CurrentStepIndex + 1} of {_steps.Count}";
    public bool CanGoBack => CurrentStepIndex > 0 && CurrentStepIndex < _steps.Count - 1;
    public bool IsLastStep => CurrentStepIndex == _steps.Count - 2; // step before progress
    public bool IsOnProgressStep => CurrentStepIndex == _steps.Count - 1;
    public string NextLabel => IsLastStep ? "Begin Setup" : "Next →";

    public bool CurrentStepCanProceed => CurrentStep switch
    {
        WelcomeStepViewModel s      => s.CanProceed,
        RequirementsStepViewModel s => s.CanProceed,
        InstallPathStepViewModel s  => s.CanProceed,
        TokenStepViewModel s        => s.CanProceed,
        VmConfigStepViewModel s     => s.CanProceed,
        BattlegroupConfigStepViewModel s => s.CanProceed,
        ReviewStepViewModel s       => s.CanProceed,
        ProgressStepViewModel s     => s.CanProceed,
        _ => true
    };

    public SetupWizardViewModel(
        IServiceScopeFactory scopeFactory,
        InstallationOrchestrator orchestrator,
        RequirementsStepViewModel requirementsStep)
    {
        _scopeFactory = scopeFactory;
        _orchestrator = orchestrator;

        _steps =
        [
            new WelcomeStepViewModel(),
            requirementsStep,
            new InstallPathStepViewModel(),
            new TokenStepViewModel(),
            new VmConfigStepViewModel(),
            new BattlegroupConfigStepViewModel(),
            new ReviewStepViewModel(),
            new ProgressStepViewModel(),
        ];

        // Forward CanProceed changes from each step
        foreach (var step in _steps)
            step.PropertyChanged += (_, _) => OnPropertyChanged(nameof(CurrentStepCanProceed));
    }

    public async Task InitializeAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ISetupWizardStateRepository>();
        var saved = await repo.GetActiveAsync();
        if (saved is not null)
        {
            _state = saved;
            HasResumeOffer = true;
        }
    }

    [RelayCommand]
    public async Task ResumeAsync()
    {
        HasResumeOffer = false;
        RestoreStepViewModels();
        CurrentStepIndex = _state.CurrentStepIndex;
    }

    [RelayCommand]
    public async Task StartFreshAsync()
    {
        HasResumeOffer = false;
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ISetupWizardStateRepository>();
        await repo.ClearAsync();
        _state = new SetupWizardState();
        CurrentStepIndex = 0;
    }

    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private void GoBack() => CurrentStepIndex--;

    [RelayCommand]
    private async Task GoNextAsync()
    {
        if (IsOnProgressStep) return;

        SyncStateFromCurrentStep();
        _state.CurrentStepIndex = CurrentStepIndex + 1;

        if (IsLastStep)
            PopulateReviewStep();

        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ISetupWizardStateRepository>();
        await repo.SaveAsync(_state);

        CurrentStepIndex++;
        OnPropertyChanged(nameof(CurrentStepCanProceed));

        // Fire off installation when entering the Progress step
        if (IsOnProgressStep && _steps[^1] is ProgressStepViewModel progressVm)
        {
            _installCts = new CancellationTokenSource();
            _ = _orchestrator.RunAsync(_state, progressVm, _installCts.Token);
        }
    }

    private void SyncStateFromCurrentStep()
    {
        switch (CurrentStep)
        {
            case InstallPathStepViewModel s:
                _state.InstallPath = s.InstallPath;
                _state.BackupPath = s.BackupPath;
                _state.LogsPath = s.LogsPath;
                break;
            case TokenStepViewModel s:
                _state.DuneAccountToken = s.Token;
                break;
            case VmConfigStepViewModel s:
                _state.VmName = s.VmName;
                _state.VmMemoryMb = s.VmMemoryMb;
                _state.NetworkAdapterName = s.SelectedAdapterName;
                break;
            case BattlegroupConfigStepViewModel s:
                _state.BattlegroupName = s.BattlegroupName;
                _state.ServerCount = s.ServerCount;
                _state.AdminPassword = s.AdminPassword;
                break;
        }
    }

    private void PopulateReviewStep()
    {
        if (_steps[^2] is not ReviewStepViewModel review) return;
        review.InstallPath = _state.InstallPath ?? string.Empty;
        review.BackupPath = _state.BackupPath ?? string.Empty;
        review.LogsPath = _state.LogsPath ?? string.Empty;
        review.VmName = _state.VmName ?? string.Empty;
        review.VmMemory = $"{_state.VmMemoryMb / 1024.0:F1} GB";
        review.NetworkAdapter = _state.NetworkAdapterName ?? string.Empty;
        review.BattlegroupName = _state.BattlegroupName ?? string.Empty;
        review.ServerCount = _state.ServerCount;
    }

    private void RestoreStepViewModels()
    {
        if (_steps[2] is InstallPathStepViewModel install)
        {
            install.InstallPath = _state.InstallPath ?? string.Empty;
            install.BackupPath = _state.BackupPath ?? string.Empty;
            install.LogsPath = _state.LogsPath ?? string.Empty;
        }
        if (_steps[3] is TokenStepViewModel token)
            token.Token = _state.DuneAccountToken ?? string.Empty;
        if (_steps[4] is VmConfigStepViewModel vm)
        {
            vm.VmName = _state.VmName ?? vm.VmName;
            vm.VmMemoryMb = _state.VmMemoryMb;
            vm.SelectedAdapterName = _state.NetworkAdapterName ?? vm.SelectedAdapterName;
        }
        if (_steps[5] is BattlegroupConfigStepViewModel bg)
        {
            bg.BattlegroupName = _state.BattlegroupName ?? string.Empty;
            bg.ServerCount = _state.ServerCount;
            bg.AdminPassword = _state.AdminPassword ?? string.Empty;
        }
    }

    private static string StepTitle(ObservableObject step) => step switch
    {
        WelcomeStepViewModel => "Welcome",
        RequirementsStepViewModel => "Requirements",
        InstallPathStepViewModel => "Location",
        TokenStepViewModel => "Token",
        VmConfigStepViewModel => "VM Setup",
        BattlegroupConfigStepViewModel => "Battlegroup",
        ReviewStepViewModel => "Review",
        ProgressStepViewModel => "Installing",
        _ => "Step"
    };
}
