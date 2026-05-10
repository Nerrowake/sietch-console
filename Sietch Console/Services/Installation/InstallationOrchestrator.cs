using Microsoft.Extensions.DependencyInjection;
using Sietch_Console.ViewModels.Steps;
using SietchConsole.Core.Interfaces;
using SietchConsole.Core.Models;
using System.IO;

namespace Sietch_Console.Services.Installation;

/// <summary>
/// Coordinates the full installation flow: download server files via SteamCMD,
/// run the post-install setup script, and persist profile metadata.
/// </summary>
public class InstallationOrchestrator
{
    private readonly ISteamCmdService        _steamCmd;
    private readonly IServerPackageInstaller _installer;
    private readonly IServerPackageService   _packageService;
    private readonly IServiceScopeFactory    _scopeFactory;

    public InstallationOrchestrator(
        ISteamCmdService        steamCmd,
        IServerPackageInstaller installer,
        IServerPackageService   packageService,
        IServiceScopeFactory    scopeFactory)
    {
        _steamCmd       = steamCmd;
        _installer      = installer;
        _packageService = packageService;
        _scopeFactory   = scopeFactory;
    }

    public async Task RunAsync(
        SetupWizardState state,
        ProgressStepViewModel progress,
        CancellationToken cancellationToken = default)
    {
        progress.IsRunning    = true;
        progress.IsComplete   = false;
        progress.HasFailed    = false;
        progress.ProgressPercent = 0;

        try
        {
            var installPath = state.InstallPath ?? string.Empty;

            // Step 1: Download server files via SteamCMD
            progress.CurrentOperation = "Downloading server files…";
            progress.AppendLog("[1/3] Downloading Dune: Awakening server files via SteamCMD…");
            progress.AppendLog(new string('─', 60));

            var installProgress = new Progress<double>(p =>
                progress.ProgressPercent = p * 0.75);  // maps 0–100 → 0–75

            await _installer.InstallAsync(
                installPath,
                installProgress,
                line => progress.AppendLog(line),
                cancellationToken);

            progress.AppendLog(new string('─', 60));
            progress.ProgressPercent = 75;

            // Step 2: Record the installed build ID
            progress.CurrentOperation = "Recording installation metadata…";
            progress.AppendLog("[2/3] Recording installed build ID…");
            var buildId = _installer.GetInstalledBuildId(installPath);
            if (buildId is not null)
                progress.AppendLog($"      Build ID: {buildId}");

            // Step 3: Run initial-setup.bat if the server provides one
            progress.CurrentOperation = "Running post-install configuration…";
            progress.AppendLog("[3/3] Looking for initial-setup.bat…");

            var setupScript = _packageService.FindSetupScript(installPath);
            if (setupScript is not null)
            {
                progress.AppendLog($"      Found: {setupScript}");
                progress.AppendLog("      Running initial-setup.bat…");
                progress.AppendLog(new string('─', 60));

                var scriptProgress = new Progress<double>(p =>
                    progress.ProgressPercent = 75 + (p * 0.20));  // maps 0–100 → 75–95

                await RunSetupScriptAsync(
                    setupScript,
                    Path.GetDirectoryName(setupScript)!,
                    line => progress.AppendLog(line),
                    scriptProgress,
                    cancellationToken);

                progress.AppendLog(new string('─', 60));
                progress.AppendLog("      Post-install configuration complete.");
            }
            else
            {
                progress.AppendLog("      No initial-setup.bat found — skipping.");
            }

            progress.ProgressPercent = 95;

            // Persist profile and settings
            progress.CurrentOperation = "Saving configuration…";
            var battlegroupScript = _packageService.FindBattlegroupScript(installPath);
            await PersistAsync(state, installPath, battlegroupScript, buildId);

            progress.ProgressPercent    = 100;
            progress.CurrentOperation   = "Setup complete.";
            progress.IsComplete         = true;
        }
        catch (OperationCanceledException)
        {
            progress.CurrentOperation = "Setup cancelled.";
            progress.AppendLog("[cancelled] The installation was cancelled.");
            progress.HasFailed = true;
        }
        catch (Exception ex)
        {
            progress.CurrentOperation = "Setup failed — see log for details.";
            progress.AppendLog($"[error] {ex.Message}");
            progress.HasFailed = true;
        }
        finally
        {
            progress.IsRunning = false;
        }
    }

    private static async Task RunSetupScriptAsync(
        string scriptPath,
        string workingDirectory,
        Action<string> onOutputLine,
        IProgress<double> scriptProgress,
        CancellationToken cancellationToken)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName               = "cmd.exe",
            Arguments              = $"/c \"{scriptPath}\"",
            WorkingDirectory       = workingDirectory,
            UseShellExecute        = false,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            CreateNoWindow         = true,
        };

        using var process = new System.Diagnostics.Process
        {
            StartInfo          = psi,
            EnableRaisingEvents = true,
        };

        int linesRead = 0;

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            onOutputLine(e.Data);
            linesRead++;
            scriptProgress.Report(Math.Min(90.0, linesRead * 2.0));
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            onOutputLine($"[stderr] {e.Data}");
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken);
        scriptProgress.Report(100);

        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"initial-setup.bat exited with code {process.ExitCode}.");
    }

    private async Task PersistAsync(
        SetupWizardState state,
        string resolvedInstallPath,
        string? battlegroupScript,
        string? buildId)
    {
        using var scope       = _scopeFactory.CreateScope();
        var profileRepo       = scope.ServiceProvider.GetRequiredService<IBattlegroupProfileRepository>();
        var settingsRepo      = scope.ServiceProvider.GetRequiredService<IApplicationSettingsRepository>();
        var stateRepo         = scope.ServiceProvider.GetRequiredService<ISetupWizardStateRepository>();

        var profile = new BattlegroupProfile
        {
            Name              = state.BattlegroupName ?? "My Battlegroup",
            InstallPath       = resolvedInstallPath,
            VmName            = state.VmName,
            ServerPackagePath = resolvedInstallPath,
            UserSettingsPath  = battlegroupScript is not null
                ? Path.GetDirectoryName(battlegroupScript)
                : null,
            LastKnownStatus   = "Offline",
        };
        await profileRepo.AddAsync(profile);

        var settings = await settingsRepo.GetAsync();
        settings.InstallPath               = resolvedInstallPath;
        settings.BackupDirectory           = state.BackupPath;
        settings.LastOpenedBattlegroupId   = profile.Id.ToString();
        settings.InstalledBuildId          = buildId;
        await settingsRepo.SaveAsync(settings);

        state.IsComplete = true;
        await stateRepo.SaveAsync(state);
    }
}
