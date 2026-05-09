using System.Collections.ObjectModel;
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
    private BattlegroupProfile?           _profile;

    // ── State ─────────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBackups))]
    private ObservableCollection<BackupRecord> _backups = [];

    [ObservableProperty] private bool   _isLoading;
    [ObservableProperty] private string _statusMessage = string.Empty;

    public bool HasBackups => Backups.Count > 0;

    // ── Confirmation flow ─────────────────────────────────────────────
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

    public BackupsViewModel(IBackupService backupService, IServiceScopeFactory scopeFactory)
    {
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

        await LoadBackupsAsync();
    }

    // #79 – Load all backups from DB
    private async Task LoadBackupsAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IBackupRecordRepository>();
        var all  = await repo.GetAllAsync();
        Backups  = new ObservableCollection<BackupRecord>(
            all.Where(b => b.BattlegroupProfileId == _profile?.Id ||
                           _profile is null));
        OnPropertyChanged(nameof(HasBackups));
    }

    // #77 – Manual config backup
    [RelayCommand]
    private async Task BackupConfigAsync()
    {
        if (_profile is null) { StatusMessage = "No battlegroup profile configured."; return; }
        IsLoading     = true;
        StatusMessage = string.Empty;
        try
        {
            var record = await _backupService.CreateConfigBackupAsync(_profile,
                notes: $"Manual config backup");
            Backups.Insert(0, record);
            OnPropertyChanged(nameof(HasBackups));
            StatusMessage = $"Configuration backup created successfully.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Backup failed: {ex.Message}";
        }
        finally { IsLoading = false; }
    }

    // #78 – Manual save data backup
    [RelayCommand]
    private async Task BackupSaveDataAsync()
    {
        if (_profile is null) { StatusMessage = "No battlegroup profile configured."; return; }
        IsLoading     = true;
        StatusMessage = string.Empty;
        try
        {
            var record = await _backupService.CreateSaveDataBackupAsync(_profile,
                notes: "Manual save data backup");
            Backups.Insert(0, record);
            OnPropertyChanged(nameof(HasBackups));
            StatusMessage = "Save data backup created successfully.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Backup failed: {ex.Message}";
        }
        finally { IsLoading = false; }
    }

    // #80 – Request restore (shows confirmation)
    [RelayCommand]
    private void RequestRestore(BackupRecord record)
    {
        PendingRecord = record;
        PendingAction = PendingBackupAction.Restore;
    }

    // #81 – Request delete (shows confirmation)
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
                await _backupService.RestoreAsync(record);
                StatusMessage = $"Backup restored successfully. Restart the battlegroup to apply changes.";
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
