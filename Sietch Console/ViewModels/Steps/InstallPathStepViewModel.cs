using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace Sietch_Console.ViewModels.Steps;

public partial class InstallPathStepViewModel : ObservableObject
{
    public string Title => "Location";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanProceed))]
    private string _installPath = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanProceed))]
    private string _backupPath = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanProceed))]
    private string _logsPath = string.Empty;

    public bool CanProceed =>
        !string.IsNullOrWhiteSpace(InstallPath) &&
        !string.IsNullOrWhiteSpace(BackupPath) &&
        !string.IsNullOrWhiteSpace(LogsPath);

    [RelayCommand]
    private void BrowseInstallPath()
        => InstallPath = BrowseFolder("Select Server Installation Folder") ?? InstallPath;

    [RelayCommand]
    private void BrowseBackupPath()
        => BackupPath = BrowseFolder("Select Backup Folder") ?? BackupPath;

    [RelayCommand]
    private void BrowseLogsPath()
        => LogsPath = BrowseFolder("Select Logs Folder") ?? LogsPath;

    private static string? BrowseFolder(string title)
    {
        var dialog = new OpenFolderDialog { Title = title };
        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }
}
