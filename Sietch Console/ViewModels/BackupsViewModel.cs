using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using SietchConsole.Core.Interfaces;
using SietchConsole.Core.Models;

namespace Sietch_Console.ViewModels;

public enum PendingBackupAction { None, Delete, Restore }

public partial class BackupsViewModel : ObservableObject
{
    private readonly IBackupService       _backupService;
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

    // ── Constructor ───────────────────────────────────────────────────────────

    public BackupsViewModel(IBackupService backupService, IServiceScopeFactory scopeFactory,
                            IActiveProfileService activeProfileService)
    {
        _backupService        = backupService;
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
}
