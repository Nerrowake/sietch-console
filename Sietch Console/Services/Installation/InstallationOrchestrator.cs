using Microsoft.Extensions.DependencyInjection;
using Sietch_Console.ViewModels.Steps;
using SietchConsole.Core.Interfaces;
using SietchConsole.Core.Models;
using System.IO;

namespace Sietch_Console.Services.Installation;

/// <summary>
/// Coordinates the full installation flow: detect, locate scripts, run, and persist.
/// </summary>
public class InstallationOrchestrator
{
    private readonly ISteamDetectionService _steam;
    private readonly IServerPackageService _server;
    private readonly ISetupScriptService _scriptService;
    private readonly IServiceScopeFactory _scopeFactory;

    public InstallationOrchestrator(
        ISteamDetectionService steam,
        IServerPackageService server,
        ISetupScriptService scriptService,
        IServiceScopeFactory scopeFactory)
    {
        _steam = steam;
        _server = server;
        _scriptService = scriptService;
        _scopeFactory = scopeFactory;
    }

    public async Task RunAsync(
        SetupWizardState state,
        ProgressStepViewModel progress,
        CancellationToken cancellationToken = default)
    {
        progress.IsRunning = true;
        progress.IsComplete = false;
        progress.HasFailed = false;
        progress.ProgressPercent = 0;

        try
        {
            // Step 1: Detect Steam
            progress.CurrentOperation = "Detecting Steam installation…";
            progress.AppendLog("[1/5] Looking for Steam…");
            var steamPath = _steam.DetectSteamPath();
            if (steamPath is null)
                progress.AppendLog("      Steam not found via registry — using install path directly.");
            else
                progress.AppendLog($"      Steam found at: {steamPath}");
            progress.ProgressPercent = 10;

            // Step 2: Detect or confirm server package
            progress.CurrentOperation = "Locating Dune server package…";
            progress.AppendLog("[2/5] Checking server package…");

            var installPath = state.InstallPath ?? string.Empty;

            if (!_server.IsServerPackagePresent(installPath) && steamPath is not null)
            {
                progress.AppendLog("      Not found at install path — searching Steam libraries…");
                var libraries = _steam.GetLibraryFolders(steamPath);
                var located = _server.LocateInSteamLibraries(libraries);
                if (located is not null)
                {
                    installPath = located;
                    progress.AppendLog($"      Found in Steam library: {located}");
                }
                else
                {
                    progress.AppendLog("      Server package not found. Proceeding with configured path.");
                }
            }
            else
            {
                progress.AppendLog($"      Server package found at: {installPath}");
            }
            progress.ProgressPercent = 25;

            // Step 3: Locate setup script
            progress.CurrentOperation = "Locating setup scripts…";
            progress.AppendLog("[3/5] Searching for initial-setup.bat…");
            var setupScript = _server.FindSetupScript(installPath);
            if (setupScript is null)
                throw new FileNotFoundException(
                    "initial-setup.bat not found within the server package. " +
                    "Ensure the Dune: Awakening dedicated server is installed.");
            progress.AppendLog($"      Found: {setupScript}");

            var battlegroupScript = _server.FindBattlegroupScript(installPath);
            progress.AppendLog(battlegroupScript is not null
                ? $"      battlegroup.bat found: {battlegroupScript}"
                : "      battlegroup.bat not found yet (may be created by setup).");
            progress.ProgressPercent = 35;

            // Step 4: Run initial-setup.bat
            progress.CurrentOperation = "Running initial-setup.bat…";
            progress.AppendLog("[4/5] Executing initial-setup.bat…");
            progress.AppendLog(new string('─', 60));

            var scriptProgress = new Progress<double>(p =>
            {
                progress.ProgressPercent = 35 + (p * 0.55); // maps 0–100 → 35–90
            });

            await _scriptService.RunAsync(
                setupScript,
                Path.GetDirectoryName(setupScript)!,
                line => progress.AppendLog(line),
                scriptProgress,
                cancellationToken);

            progress.AppendLog(new string('─', 60));
            progress.AppendLog("      Setup script completed successfully.");
            progress.ProgressPercent = 90;

            // Step 5: Persist metadata
            progress.CurrentOperation = "Saving installation metadata…";
            progress.AppendLog("[5/5] Persisting installation metadata…");

            await PersistAsync(state, installPath, setupScript, battlegroupScript);

            progress.AppendLog("      Metadata saved.");
            progress.ProgressPercent = 100;
            progress.CurrentOperation = "Setup complete.";
            progress.IsComplete = true;
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

    private async Task PersistAsync(
        SetupWizardState state,
        string resolvedInstallPath,
        string setupScript,
        string? battlegroupScript)
    {
        using var scope = _scopeFactory.CreateScope();
        var profileRepo = scope.ServiceProvider.GetRequiredService<IBattlegroupProfileRepository>();
        var settingsRepo = scope.ServiceProvider.GetRequiredService<IApplicationSettingsRepository>();
        var stateRepo = scope.ServiceProvider.GetRequiredService<ISetupWizardStateRepository>();

        // Create BattlegroupProfile from wizard config
        var profile = new BattlegroupProfile
        {
            Name = state.BattlegroupName ?? "My Battlegroup",
            InstallPath = resolvedInstallPath,
            VmName = state.VmName,
            ServerPackagePath = resolvedInstallPath,
            UserSettingsPath = battlegroupScript is not null
                ? Path.GetDirectoryName(battlegroupScript)
                : null,
            LastKnownStatus = "Offline",
        };
        await profileRepo.AddAsync(profile);

        // Update application settings with install path and backup directory
        var settings = await settingsRepo.GetAsync();
        settings.InstallPath = resolvedInstallPath;
        settings.BackupDirectory = state.BackupPath;
        settings.LastOpenedBattlegroupId = profile.Id.ToString();
        await settingsRepo.SaveAsync(settings);

        // Mark wizard state complete
        state.IsComplete = true;
        await stateRepo.SaveAsync(state);
    }
}
