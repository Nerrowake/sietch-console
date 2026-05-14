using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Sietch_Console.Services.Cloud;
using SietchConsole.Core.Interfaces;
using SietchConsole.Core.Models;

namespace Sietch_Console.ViewModels;

public enum PendingBackupAction { None, Delete, Restore }

public partial class BackupsViewModel : ObservableObject
{
    private readonly IBackupService       _backupService;
    private readonly ICloudSyncService    _cloudSync;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IActiveProfileService _activeProfileService;
    private readonly DispatcherTimer      _autoBackupTimer;

    private BattlegroupProfile? _profile;

    // ── Backup list ───────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBackups))]
    private ObservableCollection<BackupRecord> _backups = [];

    [ObservableProperty] private bool   _isLoading;
    [ObservableProperty] private string _statusMessage = string.Empty;

    public bool HasBackups => Backups.Count > 0;

    // ── Confirmation flow ─────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowConfirmation))]
    [NotifyPropertyChangedFor(nameof(ConfirmationTitle))]
    [NotifyPropertyChangedFor(nameof(ConfirmationBody))]
    private PendingBackupAction _pendingAction = PendingBackupAction.None;

    [ObservableProperty] private BackupRecord? _pendingRecord;

    public bool ShowConfirmation => PendingAction != PendingBackupAction.None;

    public string ConfirmationTitle => PendingAction == PendingBackupAction.Delete
        ? "Delete Backup?"
        : "Restore Backup?";

    public string ConfirmationBody => PendingAction == PendingBackupAction.Delete
        ? "This will permanently delete the backup files from disk. This cannot be undone."
        : "This will overwrite the current configuration or save data with the selected backup. The server should be stopped first.";

    // ── Auto-backup settings (#136, #138) ────────────────────────────────────

    [ObservableProperty]
    private bool _autoBackupEnabled;

    [ObservableProperty]
    private int _backupIntervalHours = 6;

    [ObservableProperty]
    private int _backupRetainCount = 10;

    // Interval choices shown in the ComboBox (hours)
    public IReadOnlyList<int> IntervalChoices { get; } = [1, 3, 6, 12, 24];

    partial void OnAutoBackupEnabledChanged(bool value)   => ReconfigureTimer();
    partial void OnBackupIntervalHoursChanged(int value)  => ReconfigureTimer();

    // ── Cloud sync settings (#173, #174, #175, #176) ──────────────────────────

    public IReadOnlyList<string> CloudProviders { get; } = ["None", "OneDrive", "S3"];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowOneDriveFields))]
    [NotifyPropertyChangedFor(nameof(ShowS3Fields))]
    private string _cloudProvider = "None";

    [ObservableProperty] private bool   _cloudEnabled;
    [ObservableProperty] private string _cloudFolderPath     = "SietchConsole/Backups";
    [ObservableProperty] private string _s3Bucket            = string.Empty;
    [ObservableProperty] private string _s3Region            = "us-east-1";
    [ObservableProperty] private string _s3Endpoint          = string.Empty;
    [ObservableProperty] private string _s3AccessKeyId       = string.Empty;
    [ObservableProperty] private string _s3SecretKey         = string.Empty;   // plain text while editing; encrypted before save
    [ObservableProperty] private string _cloudStatusMessage  = string.Empty;
    [ObservableProperty] private bool   _cloudIsBusy;
    [ObservableProperty] private double _cloudProgress;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCloudBackups))]
    private ObservableCollection<CloudBackupFile> _cloudBackups = [];

    public bool HasCloudBackups    => CloudBackups.Count > 0;
    public bool ShowOneDriveFields => CloudProvider == "OneDrive";
    public bool ShowS3Fields       => CloudProvider == "S3";

    // ── Constructor ───────────────────────────────────────────────────────────

    public BackupsViewModel(IBackupService backupService, ICloudSyncService cloudSync,
                            IServiceScopeFactory scopeFactory,
                            IActiveProfileService activeProfileService)
    {
        _backupService        = backupService;
        _cloudSync            = cloudSync;
        _scopeFactory         = scopeFactory;
        _activeProfileService = activeProfileService;

        _autoBackupTimer = new DispatcherTimer();
        _autoBackupTimer.Tick += async (_, _) => await RunAutoBackupAsync();

        _activeProfileService.ProfileChanged += (_, profile) =>
        {
            _profile = profile;
            _ = LoadBackupsAsync();
        };
    }

    // ── Initialise ────────────────────────────────────────────────────────────

    public async Task InitializeAsync()
    {
        _profile = _activeProfileService.Current;

        using var scope      = _scopeFactory.CreateScope();
        var settingsRepo     = scope.ServiceProvider.GetRequiredService<IApplicationSettingsRepository>();
        var settings         = await settingsRepo.GetAsync();

        // Load persisted auto-backup settings
        AutoBackupEnabled   = settings.AutoBackupEnabled;
        BackupIntervalHours = settings.BackupIntervalHours > 0 ? settings.BackupIntervalHours : 6;
        BackupRetainCount   = settings.BackupRetainCount   > 0 ? settings.BackupRetainCount   : 10;

        // Load cloud sync settings
        await LoadCloudSettingsAsync(settings);

        await LoadBackupsAsync();
        ReconfigureTimer();
    }

    // ── Load backups (#79) ────────────────────────────────────────────────────

    private async Task LoadBackupsAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IBackupRecordRepository>();
        var all  = await repo.GetAllAsync();
        Backups  = new ObservableCollection<BackupRecord>(
            all.Where(b => b.BattlegroupProfileId == _profile?.Id || _profile is null)
               .OrderByDescending(b => b.CreatedAt));
        OnPropertyChanged(nameof(HasBackups));
    }

    // ── Manual backups ────────────────────────────────────────────────────────

    // #77 – Config backup
    [RelayCommand]
    private async Task BackupConfigAsync()
    {
        if (_profile is null) { StatusMessage = "No battlegroup profile configured."; return; }
        await RunBackupAsync(() => _backupService.CreateConfigBackupAsync(_profile, "Manual config backup"),
            "Configuration backup created.");
    }

    // #78 – Save data backup (#134)
    [RelayCommand]
    private async Task BackupSaveDataAsync()
    {
        if (_profile is null) { StatusMessage = "No battlegroup profile configured."; return; }
        await RunBackupAsync(() => _backupService.CreateSaveDataBackupAsync(_profile, "Manual save data backup"),
            "Save data backup created.");
    }

    // #135 – Full backup
    [RelayCommand]
    private async Task BackupFullAsync()
    {
        if (_profile is null) { StatusMessage = "No battlegroup profile configured."; return; }
        await RunBackupAsync(() => _backupService.CreateFullBackupAsync(_profile, "Manual full backup"),
            "Full backup created.");
    }

    private async Task RunBackupAsync(Func<Task<BackupRecord>> createFn, string successMessage)
    {
        IsLoading     = true;
        StatusMessage = string.Empty;
        try
        {
            var record = await createFn();
            Backups.Insert(0, record);
            OnPropertyChanged(nameof(HasBackups));
            StatusMessage = successMessage;

            // Prune after each backup to stay within the retention limit (#136)
            if (_profile is not null && BackupRetainCount > 0)
            {
                await _backupService.PruneOldBackupsAsync(_profile, BackupRetainCount);
                await LoadBackupsAsync();   // refresh list after pruning
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Backup failed: {ex.Message}";
        }
        finally { IsLoading = false; }
    }

    // ── Auto-backup (#138) ────────────────────────────────────────────────────

    private void ReconfigureTimer()
    {
        _autoBackupTimer.Stop();
        if (!AutoBackupEnabled || BackupIntervalHours <= 0) return;
        _autoBackupTimer.Interval = TimeSpan.FromHours(BackupIntervalHours);
        _autoBackupTimer.Start();
    }

    private async Task RunAutoBackupAsync()
    {
        if (_profile is null) return;
        await RunBackupAsync(
            () => _backupService.CreateFullBackupAsync(_profile, "Automatic scheduled backup"),
            "Auto-backup completed.");
    }

    // #138 – Save auto-backup settings to the database
    [RelayCommand]
    private async Task SaveAutoBackupSettingsAsync()
    {
        using var scope  = _scopeFactory.CreateScope();
        var settingsRepo = scope.ServiceProvider.GetRequiredService<IApplicationSettingsRepository>();
        var settings     = await settingsRepo.GetAsync();

        settings.AutoBackupEnabled   = AutoBackupEnabled;
        settings.BackupIntervalHours = BackupIntervalHours;
        settings.BackupRetainCount   = BackupRetainCount;
        await settingsRepo.SaveAsync(settings);

        StatusMessage = "Auto-backup settings saved.";
        ReconfigureTimer();
    }

    // ── Restore (#80) ─────────────────────────────────────────────────────────

    [RelayCommand]
    private void RequestRestore(BackupRecord record)
    {
        PendingRecord = record;
        PendingAction = PendingBackupAction.Restore;
    }

    // ── Delete (#81) ──────────────────────────────────────────────────────────

    [RelayCommand]
    private void RequestDelete(BackupRecord record)
    {
        PendingRecord = record;
        PendingAction = PendingBackupAction.Delete;
    }

    [RelayCommand]
    private async Task ConfirmActionAsync()
    {
        var action = PendingAction;
        var record = PendingRecord;
        PendingAction = PendingBackupAction.None;
        PendingRecord = null;

        if (record is null) return;
        IsLoading     = true;
        StatusMessage = string.Empty;
        try
        {
            if (action == PendingBackupAction.Delete)
            {
                await _backupService.DeleteAsync(record);
                Backups.Remove(record);
                OnPropertyChanged(nameof(HasBackups));
                StatusMessage = "Backup deleted.";
            }
            else
            {
                StatusMessage = "Restoring backup…";
                await _backupService.RestoreAsync(record);
                StatusMessage = "Backup restored. Restart the battlegroup to apply changes.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Operation failed: {ex.Message}";
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private void CancelAction()
    {
        PendingAction = PendingBackupAction.None;
        PendingRecord = null;
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadBackupsAsync();

    // ── Cloud sync commands (#173, #174, #175, #176) ──────────────────────────

    [RelayCommand]
    private async Task SaveCloudSettingsAsync()
    {
        CloudIsBusy      = true;
        CloudStatusMessage = string.Empty;
        try
        {
            using var scope  = _scopeFactory.CreateScope();
            var settingsRepo = scope.ServiceProvider.GetRequiredService<IApplicationSettingsRepository>();
            var settings     = await settingsRepo.GetAsync();

            settings.CloudSyncEnabled    = CloudEnabled;
            settings.CloudSyncProvider   = CloudProvider;
            settings.CloudSyncFolderPath = CloudFolderPath.Trim();
            settings.S3BucketName        = S3Bucket.Trim();
            settings.S3Region            = S3Region.Trim();
            settings.S3EndpointUrl       = string.IsNullOrWhiteSpace(S3Endpoint) ? null : S3Endpoint.Trim();
            settings.S3AccessKeyId       = S3AccessKeyId.Trim();

            if (!string.IsNullOrWhiteSpace(S3SecretKey))
                settings.S3EncryptedSecretKey = CloudSyncService.EncryptSecretKey(S3SecretKey.Trim());

            await settingsRepo.SaveAsync(settings);
            CloudStatusMessage = "Cloud sync settings saved.";
        }
        catch (Exception ex)
        {
            CloudStatusMessage = $"Save failed: {ex.Message}";
        }
        finally { CloudIsBusy = false; }
    }

    [RelayCommand]
    private async Task TestCloudConnectionAsync()
    {
        CloudIsBusy       = true;
        CloudStatusMessage = "Testing connection…";
        try
        {
            // Save current form values first so TestConnectionAsync uses the latest input
            await SaveCloudSettingsAsync();
            var (ok, error) = await _cloudSync.TestConnectionAsync();
            CloudStatusMessage = ok ? "Connection successful." : $"Connection failed: {error}";
        }
        catch (Exception ex)
        {
            CloudStatusMessage = $"Error: {ex.Message}";
        }
        finally { CloudIsBusy = false; }
    }

    [RelayCommand]
    private async Task SyncToCloudAsync(BackupRecord record)
    {
        if (!_cloudSync.IsConfigured)
        {
            CloudStatusMessage = "Cloud sync is not enabled. Configure a provider below.";
            return;
        }

        CloudIsBusy        = true;
        CloudProgress      = 0;
        CloudStatusMessage = $"Uploading {record.BackupType} backup…";
        try
        {
            var prog = new Progress<double>(p => CloudProgress = p);
            var id   = await _cloudSync.UploadBackupAsync(record, prog);
            CloudStatusMessage = "Upload complete.";

            // Refresh backup list so the cloud icon appears
            await LoadBackupsAsync();
        }
        catch (Exception ex)
        {
            CloudStatusMessage = $"Upload failed: {ex.Message}";
        }
        finally { CloudIsBusy = false; }
    }

    [RelayCommand]
    private async Task RefreshCloudBackupsAsync()
    {
        if (!_cloudSync.IsConfigured) { CloudStatusMessage = "Cloud sync is not configured."; return; }

        CloudIsBusy        = true;
        CloudStatusMessage = string.Empty;
        try
        {
            var files   = await _cloudSync.ListCloudBackupsAsync();
            CloudBackups = new ObservableCollection<CloudBackupFile>(files);
            OnPropertyChanged(nameof(HasCloudBackups));
            CloudStatusMessage = files.Count == 0 ? "No cloud backups found." : string.Empty;
        }
        catch (Exception ex)
        {
            CloudStatusMessage = $"Failed to list cloud backups: {ex.Message}";
        }
        finally { CloudIsBusy = false; }
    }

    [RelayCommand]
    private async Task DownloadAndRestoreCloudBackupAsync(CloudBackupFile file)
    {
        CloudIsBusy        = true;
        CloudProgress      = 0;
        CloudStatusMessage = $"Downloading {file.FileName}…";
        try
        {
            var prog = new Progress<double>(p => CloudProgress = p);
            await _cloudSync.DownloadAndRestoreAsync(file, prog);
            CloudStatusMessage = "Restore complete. Restart the battlegroup to apply changes.";
        }
        catch (Exception ex)
        {
            CloudStatusMessage = $"Restore failed: {ex.Message}";
        }
        finally { CloudIsBusy = false; }
    }

    [RelayCommand]
    private async Task DeleteCloudBackupAsync(CloudBackupFile file)
    {
        CloudIsBusy        = true;
        CloudStatusMessage = string.Empty;
        try
        {
            await _cloudSync.DeleteCloudBackupAsync(file);
            CloudBackups.Remove(file);
            OnPropertyChanged(nameof(HasCloudBackups));
            CloudStatusMessage = "Cloud backup deleted.";
        }
        catch (Exception ex)
        {
            CloudStatusMessage = $"Delete failed: {ex.Message}";
        }
        finally { CloudIsBusy = false; }
    }

    // ── Cloud settings helpers ────────────────────────────────────────────────

    private async Task LoadCloudSettingsAsync(ApplicationSettings settings)
    {
        CloudEnabled    = settings.CloudSyncEnabled;
        CloudProvider   = settings.CloudSyncProvider ?? "None";
        CloudFolderPath = settings.CloudSyncFolderPath ?? "SietchConsole/Backups";
        S3Bucket        = settings.S3BucketName   ?? string.Empty;
        S3Region        = settings.S3Region        ?? "us-east-1";
        S3Endpoint      = settings.S3EndpointUrl   ?? string.Empty;
        S3AccessKeyId   = settings.S3AccessKeyId   ?? string.Empty;
        // Secret key is never loaded back into the text field — user must re-enter to change it.
        S3SecretKey     = string.Empty;

        await Task.CompletedTask;
    }
}
