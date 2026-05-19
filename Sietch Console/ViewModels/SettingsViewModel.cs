using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using SietchConsole.Core.Interfaces;
using SietchConsole.Core.Models;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;

namespace Sietch_Console.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IConfigurationService    _configService;
    private readonly IBackupService           _backupService;
    private readonly IServiceScopeFactory     _scopeFactory;
    private readonly IActiveProfileService    _activeProfileService;
    private readonly IRemoteManagementService _remoteService;
    private readonly IDiscordWebhookService   _discordService;
    private readonly ISshService              _sshService;

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

    // Raw sync warning: shown when both the structured form and raw editor have unsaved changes
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowRawSyncWarning))]
    private bool _rawSyncWarningDismissed;

    public bool ShowRawSyncWarning =>
        HasUnsavedChanges && RawHasUnsavedChanges && !RawSyncWarningDismissed;

    // ── VM Connection (#177, #178) ────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SshKeyPathDisplay))]
    private string _vmIpAddress = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SshKeyPathDisplay))]
    private string _vmSshKeyPath = string.Empty;   // empty = use default

    [ObservableProperty] private string _vmUsername    = "dune";
    [ObservableProperty] private int    _vmSshPort     = 22;
    [ObservableProperty] private string _vmSshStatusMessage = string.Empty;
    [ObservableProperty] private bool   _vmSshIsBusy;

    /// <summary>Displayed placeholder when the path field is empty (uses the known default).</summary>
    public string SshKeyPathDisplay => string.IsNullOrWhiteSpace(VmSshKeyPath)
        ? BattlegroupProfile.DefaultSshKeyPath
        : VmSshKeyPath;

    // ── Remote management (#152, #151) ────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSaveRemote))]
    private bool _remoteEnabled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSaveRemote))]
    private int _remotePort = 5151;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSaveRemote))]
    private string _remoteToken = string.Empty;

    [ObservableProperty] private bool    _remoteIsRunning;
    [ObservableProperty] private string? _remoteLiveUrl;
    [ObservableProperty] private string  _remoteStatusMessage = string.Empty;
    [ObservableProperty] private bool    _remoteIsBusy;

    public bool CanSaveRemote => !RemoteIsBusy && RemotePort is >= 1024 and <= 65535;

    // ── Discord webhook settings (#170, #171, #172) ───────────────────────────
    [ObservableProperty] private bool   _discordEnabled;
    [ObservableProperty] private string _discordWebhookUrl    = string.Empty;
    [ObservableProperty] private bool   _discordNotifyStart   = true;
    [ObservableProperty] private bool   _discordNotifyStop    = true;
    [ObservableProperty] private bool   _discordNotifyCrash   = true;
    [ObservableProperty] private string _discordStatusMessage = string.Empty;
    [ObservableProperty] private bool   _discordIsBusy;

    public SettingsViewModel(IConfigurationService configService, IBackupService backupService,
                             IServiceScopeFactory scopeFactory, IActiveProfileService activeProfileService,
                             IRemoteManagementService remoteService, IDiscordWebhookService discordService,
                             ISshService sshService)
    {
        _configService        = configService;
        _backupService        = backupService;
        _scopeFactory         = scopeFactory;
        _activeProfileService = activeProfileService;
        _remoteService        = remoteService;
        _discordService       = discordService;
        _sshService           = sshService;

        _activeProfileService.ProfileChanged += (_, profile) =>
        {
            _profile = profile;
            _loaded  = false;
            _ = LoadConfigAsync();
            LoadVmConnectionSettings();
        };

        _remoteService.StatusChanged += (_, _) => SyncRemoteRunningState();
    }

    public async Task InitializeAsync()
    {
        _profile = _activeProfileService.Current;
        await LoadConfigAsync();
        await LoadRemoteSettingsAsync();
        await LoadDiscordSettingsAsync();
        LoadVmConnectionSettings();
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
        if (SelectedFile is not null)
        {
            RawHasUnsavedChanges = true;
            OnPropertyChanged(nameof(ShowRawSyncWarning));
        }
    }

    partial void OnHasUnsavedChangesChanged(bool value) =>
        OnPropertyChanged(nameof(ShowRawSyncWarning));

    [RelayCommand]
    private void DismissRawSyncWarning()
    {
        RawSyncWarningDismissed = true;
        OnPropertyChanged(nameof(ShowRawSyncWarning));
    }

    // ── Remote management commands (#152, #151) ──────────────────────

    [RelayCommand]
    private void GenerateToken()
    {
        var bytes = new byte[24];
        RandomNumberGenerator.Fill(bytes);
        RemoteToken = Convert.ToBase64String(bytes);
    }

    [RelayCommand(CanExecute = nameof(CanSaveRemote))]
    private async Task SaveRemoteSettingsAsync()
    {
        if (RemoteToken.Length < 16)
        {
            RemoteStatusMessage = "Token must be at least 16 characters. Use Generate to create one.";
            return;
        }

        RemoteIsBusy = true;
        RemoteStatusMessage = string.Empty;

        try
        {
            using var scope    = _scopeFactory.CreateScope();
            var settingsRepo   = scope.ServiceProvider.GetRequiredService<IApplicationSettingsRepository>();
            var settings       = await settingsRepo.GetAsync();

            settings.RemoteManagementEnabled = RemoteEnabled;
            settings.RemoteManagementPort    = RemotePort;
            settings.RemoteManagementToken   = RemoteToken;
            await settingsRepo.SaveAsync(settings);

            // Apply change: start or stop the web server as needed.
            if (RemoteEnabled)
            {
                await _remoteService.StopAsync();
                await _remoteService.StartAsync(RemotePort, RemoteToken);
                RemoteStatusMessage = _remoteService.IsRunning
                    ? $"Web server started — {_remoteService.ListenUrl}"
                    : "Failed to start web server. Check App Logs for details.";
            }
            else
            {
                await _remoteService.StopAsync();
                RemoteStatusMessage = "Remote management disabled.";
            }

            SyncRemoteRunningState();
        }
        finally
        {
            RemoteIsBusy = false;
        }
    }

    private async Task LoadRemoteSettingsAsync()
    {
        using var scope  = _scopeFactory.CreateScope();
        var settingsRepo = scope.ServiceProvider.GetRequiredService<IApplicationSettingsRepository>();
        var settings     = await settingsRepo.GetAsync();

        RemoteEnabled = settings.RemoteManagementEnabled;
        RemotePort    = settings.RemoteManagementPort;
        RemoteToken   = settings.RemoteManagementToken ?? string.Empty;

        SyncRemoteRunningState();
    }

    private void SyncRemoteRunningState()
    {
        RemoteIsRunning = _remoteService.IsRunning;
        RemoteLiveUrl   = _remoteService.ListenUrl;
    }

    // ── Discord webhook commands (#170, #171, #172) ──────────────────────────

    [RelayCommand]
    private async Task SaveDiscordSettingsAsync()
    {
        DiscordIsBusy       = true;
        DiscordStatusMessage = string.Empty;

        try
        {
            using var scope  = _scopeFactory.CreateScope();
            var settingsRepo = scope.ServiceProvider.GetRequiredService<IApplicationSettingsRepository>();
            var settings     = await settingsRepo.GetAsync();

            settings.DiscordWebhookEnabled   = DiscordEnabled;
            settings.DiscordWebhookUrl       = DiscordWebhookUrl.Trim();
            settings.DiscordNotifyServerStart = DiscordNotifyStart;
            settings.DiscordNotifyServerStop  = DiscordNotifyStop;
            settings.DiscordNotifyServerCrash = DiscordNotifyCrash;

            await settingsRepo.SaveAsync(settings);
            DiscordStatusMessage = "Discord settings saved.";
        }
        catch (Exception ex)
        {
            DiscordStatusMessage = $"Save failed: {ex.Message}";
        }
        finally
        {
            DiscordIsBusy = false;
        }
    }

    [RelayCommand]
    private async Task TestDiscordWebhookAsync()
    {
        if (string.IsNullOrWhiteSpace(DiscordWebhookUrl))
        {
            DiscordStatusMessage = "Enter a webhook URL before testing.";
            return;
        }

        DiscordIsBusy       = true;
        DiscordStatusMessage = "Sending test message…";

        try
        {
            var (success, error) = await _discordService.TestWebhookAsync(DiscordWebhookUrl.Trim());
            DiscordStatusMessage = success
                ? "Test message delivered successfully."
                : $"Test failed: {error}";
        }
        finally
        {
            DiscordIsBusy = false;
        }
    }

    private async Task LoadDiscordSettingsAsync()
    {
        using var scope  = _scopeFactory.CreateScope();
        var settingsRepo = scope.ServiceProvider.GetRequiredService<IApplicationSettingsRepository>();
        var settings     = await settingsRepo.GetAsync();

        DiscordEnabled    = settings.DiscordWebhookEnabled;
        DiscordWebhookUrl = settings.DiscordWebhookUrl ?? string.Empty;
        DiscordNotifyStart = settings.DiscordNotifyServerStart;
        DiscordNotifyStop  = settings.DiscordNotifyServerStop;
        DiscordNotifyCrash = settings.DiscordNotifyServerCrash;
    }

    // ── VM Connection commands (#177, #178) ──────────────────────────

    [RelayCommand]
    private async Task TestSshConnectionAsync()
    {
        if (string.IsNullOrWhiteSpace(VmIpAddress))
        {
            VmSshStatusMessage = "Enter a VM IP address before testing.";
            return;
        }

        VmSshIsBusy        = true;
        VmSshStatusMessage  = "Testing connection…";

        try
        {
            var keyPath = string.IsNullOrWhiteSpace(VmSshKeyPath)
                ? BattlegroupProfile.DefaultSshKeyPath
                : VmSshKeyPath;

            if (!File.Exists(keyPath))
            {
                VmSshStatusMessage = $"SSH key not found at: {keyPath}. Run the battlegroup initial-setup first.";
                return;
            }

            var (success, error) = await _sshService.TestConnectionAsync(
                VmIpAddress.Trim(), VmSshPort, VmUsername.Trim(), keyPath);

            VmSshStatusMessage = success
                ? "Connection successful — the VM is reachable."
                : $"Connection failed: {error}";
        }
        catch (Exception ex)
        {
            VmSshStatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            VmSshIsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveVmConnectionAsync()
    {
        if (_profile is null) return;

        VmSshIsBusy        = true;
        VmSshStatusMessage  = string.Empty;

        try
        {
            _profile.VmIpAddress  = VmIpAddress.Trim();
            _profile.VmSshKeyPath = string.IsNullOrWhiteSpace(VmSshKeyPath) ? null : VmSshKeyPath.Trim();
            _profile.VmUsername   = string.IsNullOrWhiteSpace(VmUsername) ? "dune" : VmUsername.Trim();
            _profile.VmSshPort    = VmSshPort;
            _profile.UpdatedAt    = DateTime.UtcNow;

            using var scope  = _scopeFactory.CreateScope();
            var profileRepo  = scope.ServiceProvider.GetRequiredService<IBattlegroupProfileRepository>();
            await profileRepo.UpdateAsync(_profile);

            VmSshStatusMessage = "VM connection settings saved.";
        }
        catch (Exception ex)
        {
            VmSshStatusMessage = $"Save failed: {ex.Message}";
        }
        finally
        {
            VmSshIsBusy = false;
        }
    }

    [RelayCommand]
    private void BrowseSshKeyPath()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title  = "Select SSH Private Key",
            Filter = "All Files (*)|*|OpenSSH Key (sshKey)|sshKey",
        };
        if (dlg.ShowDialog() == true)
            VmSshKeyPath = dlg.FileName;
    }

    private void LoadVmConnectionSettings()
    {
        if (_profile is null) return;
        VmIpAddress  = _profile.VmIpAddress  ?? string.Empty;
        VmSshKeyPath = _profile.VmSshKeyPath ?? string.Empty;
        VmUsername   = _profile.VmUsername;
        VmSshPort    = _profile.VmSshPort;
        VmSshStatusMessage = string.Empty;
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
