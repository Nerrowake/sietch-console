using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using SietchConsole.Core.Interfaces;
using SietchConsole.Core.Models;

namespace Sietch_Console.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IConfigurationService _configService;
    private readonly IBackupService        _backupService;
    private readonly IServiceScopeFactory  _scopeFactory;

    private BattlegroupProfile? _profile;
    private bool _loaded;

    // ── Loading state ─────────────────────────────────────────────────
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _hasConfig;
    [ObservableProperty] private string? _configDirectory;
    [ObservableProperty] private string _statusMessage = string.Empty;

    // ── Change tracking ───────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    private bool _hasUnsavedChanges;

    // #65 – Restart required notification
    [ObservableProperty] private bool _restartRequired;

    // ── Validation ────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasValidationErrors))]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    private IReadOnlyList<string> _validationErrors = [];

    public bool HasValidationErrors => ValidationErrors.Count > 0;
    public bool CanSave => HasUnsavedChanges && !HasValidationErrors;

    // ── Server settings ───────────────────────────────────────────────
    [ObservableProperty] private string _serverName = string.Empty;
    [ObservableProperty] private int    _maxPlayers = 20;
    [ObservableProperty] private string _serverPassword = string.Empty;
    [ObservableProperty] private string _adminPassword = string.Empty;

    // ── Network ───────────────────────────────────────────────────────
    [ObservableProperty] private int _gamePort = 7777;

    // ── Gameplay ──────────────────────────────────────────────────────
    [ObservableProperty] private bool  _pvpEnabled = false;
    [ObservableProperty] private float _dayLengthMultiplier = 1.0f;
    [ObservableProperty] private float _nightLengthMultiplier = 1.0f;
    [ObservableProperty] private int   _maxTribeMemberCount = 10;
    [ObservableProperty] private float _resourceHarvestingRate = 1.0f;
    [ObservableProperty] private float _xpMultiplier = 1.0f;

    // ── Raw editor (#67) ──────────────────────────────────────────────
    [ObservableProperty] private IReadOnlyList<string> _availableFiles = [];
    [ObservableProperty] private string? _selectedFile;
    [ObservableProperty] private string  _rawContent = string.Empty;
    [ObservableProperty] private bool    _rawHasUnsavedChanges;

    public SettingsViewModel(IConfigurationService configService, IBackupService backupService, IServiceScopeFactory scopeFactory)
    {
        _configService = configService;
        _backupService = backupService;
        _scopeFactory  = scopeFactory;
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

        await LoadConfigAsync();
    }

    private async Task LoadConfigAsync()
    {
        IsLoading = true;
        StatusMessage = string.Empty;

        try
        {
            if (_profile is null)
            {
                HasConfig = false;
                ConfigDirectory = null;
                return;
            }

            ConfigDirectory = _configService.LocateConfigDirectory(_profile);
            HasConfig = ConfigDirectory is not null;
            AvailableFiles = await _configService.GetConfigFilesAsync(_profile);

            var config = await _configService.LoadAsync(_profile);
            if (config is not null)
                ApplyConfig(config);
        }
        finally
        {
            IsLoading = false;
            _loaded = true;
            HasUnsavedChanges = false;
            RestartRequired = false;
            ValidationErrors = [];
        }
    }

    private void ApplyConfig(BattlegroupConfig config)
    {
        _loaded = false;
        ServerName            = config.ServerName;
        MaxPlayers            = config.MaxPlayers;
        ServerPassword        = config.ServerPassword;
        AdminPassword         = config.AdminPassword;
        GamePort              = config.GamePort;
        PvpEnabled            = config.PvPEnabled;
        DayLengthMultiplier   = config.DayLengthMultiplier;
        NightLengthMultiplier = config.NightLengthMultiplier;
        MaxTribeMemberCount   = config.MaxTribeMemberCount;
        ResourceHarvestingRate= config.ResourceHarvestingRate;
        XpMultiplier          = config.XpMultiplier;
        _loaded = true;
    }

    // ── Property change hooks ─────────────────────────────────────────

    partial void OnServerNameChanged(string value)            => MarkDirty();
    partial void OnServerPasswordChanged(string value)        => MarkDirty();
    partial void OnAdminPasswordChanged(string value)         => MarkDirty();
    partial void OnMaxPlayersChanged(int value)               => MarkDirtyAndRestart();
    partial void OnGamePortChanged(int value)                 => MarkDirtyAndRestart();
    partial void OnPvpEnabledChanged(bool value)              => MarkDirtyAndRestart();
    partial void OnDayLengthMultiplierChanged(float value)    => MarkDirty();
    partial void OnNightLengthMultiplierChanged(float value)  => MarkDirty();
    partial void OnMaxTribeMemberCountChanged(int value)      => MarkDirty();
    partial void OnResourceHarvestingRateChanged(float value) => MarkDirty();
    partial void OnXpMultiplierChanged(float value)           => MarkDirty();

    partial void OnSelectedFileChanged(string? value) => _ = ReloadRawAsync();

    private void MarkDirty()
    {
        if (!_loaded) return;
        HasUnsavedChanges = true;
        RunValidation();
    }

    private void MarkDirtyAndRestart()
    {
        if (!_loaded) return;
        HasUnsavedChanges = true;
        RestartRequired = true;
        RunValidation();
    }

    private void RunValidation()
    {
        ValidationErrors = _configService.Validate(BuildConfig());
        OnPropertyChanged(nameof(CanSave));
    }

    // ── Commands ──────────────────────────────────────────────────────

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (_profile is null) return;

        RunValidation();
        if (HasValidationErrors)
        {
            StatusMessage = "Fix the errors below before saving.";
            return;
        }

        IsLoading = true;
        StatusMessage = string.Empty;
        try
        {
            await _backupService.CreateConfigBackupAsync(_profile, notes: "Auto-backup before save");
            var ok = await _configService.SaveAsync(_profile, BuildConfig());
            if (ok)
            {
                HasUnsavedChanges = false;
                StatusMessage = "Configuration saved. " +
                                (RestartRequired ? "Restart the battlegroup to apply changes." : string.Empty);
            }
            else
            {
                StatusMessage = "Save failed — validation error.";
            }
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task ReloadAsync()
    {
        _loaded = false;
        await LoadConfigAsync();
    }

    [RelayCommand]
    private async Task SaveRawAsync()
    {
        if (SelectedFile is null) return;
        await _configService.SaveRawContentAsync(SelectedFile, RawContent, createBackup: true);
        RawHasUnsavedChanges = false;
        StatusMessage = "Raw file saved. Reload the Settings page to reflect changes in the form.";
    }

    [RelayCommand]
    private async Task ReloadRawAsync()
    {
        if (SelectedFile is null) { RawContent = string.Empty; return; }
        RawContent = await _configService.GetRawContentAsync(SelectedFile);
        RawHasUnsavedChanges = false;
    }

    partial void OnRawContentChanged(string value)
    {
        if (SelectedFile is not null) RawHasUnsavedChanges = true;
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private BattlegroupConfig BuildConfig() => new()
    {
        ServerName            = ServerName,
        MaxPlayers            = MaxPlayers,
        ServerPassword        = ServerPassword,
        AdminPassword         = AdminPassword,
        GamePort              = GamePort,
        PvPEnabled            = PvpEnabled,
        DayLengthMultiplier   = DayLengthMultiplier,
        NightLengthMultiplier = NightLengthMultiplier,
        MaxTribeMemberCount   = MaxTribeMemberCount,
        ResourceHarvestingRate= ResourceHarvestingRate,
        XpMultiplier          = XpMultiplier,
    };
}
