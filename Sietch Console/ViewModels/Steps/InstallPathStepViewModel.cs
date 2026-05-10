using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace Sietch_Console.ViewModels.Steps;

public partial class InstallPathStepViewModel : ObservableObject
{
    public string Title => "Location";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanProceed))]
    [NotifyPropertyChangedFor(nameof(InstallPathError))]
    private string _installPath = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanProceed))]
    [NotifyPropertyChangedFor(nameof(BackupPathError))]
    private string _backupPath = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanProceed))]
    [NotifyPropertyChangedFor(nameof(LogsPathError))]
    private string _logsPath = string.Empty;

    // ── #179 – Inline validation errors ──────────────────────────────
    public string? InstallPathError => ValidatePath(InstallPath, "server files");
    public string? BackupPathError  => ValidatePath(BackupPath,  "backups");
    public string? LogsPathError    => ValidatePath(LogsPath,    "logs");

    public bool CanProceed =>
        !string.IsNullOrWhiteSpace(InstallPath) &&
        !string.IsNullOrWhiteSpace(BackupPath)  &&
        !string.IsNullOrWhiteSpace(LogsPath)    &&
        InstallPathError is null &&
        BackupPathError  is null &&
        LogsPathError    is null;

    private static string? ValidatePath(string path, string label)
    {
        if (string.IsNullOrWhiteSpace(path)) return null; // no error until user types

        string canonical;
        try { canonical = Path.GetFullPath(path); }
        catch { return $"The {label} path is not a valid directory path."; }

        // Reject drive roots like C:\
        if (canonical.Length <= 3 && canonical[^1] == Path.DirectorySeparatorChar)
            return $"The {label} path cannot be a drive root.";

        // Reject system-protected directories
        var winDir   = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var sysDir   = Environment.GetFolderPath(Environment.SpecialFolder.System);
        var progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var progFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

        foreach (var reserved in new[] { winDir, sysDir, progFiles, progFilesX86 })
        {
            if (!string.IsNullOrEmpty(reserved) &&
                canonical.StartsWith(reserved, StringComparison.OrdinalIgnoreCase))
                return $"The {label} path cannot be inside a system directory.";
        }

        return null;
    }

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
