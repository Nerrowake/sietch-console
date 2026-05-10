using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sietch_Console.Services.Backups;
using Sietch_Console.Services.Configuration;
using Sietch_Console.Services.Control;
using Sietch_Console.Services.Diagnostics;
using Sietch_Console.Services.Installation;
using Sietch_Console.Services.Logs;
using Sietch_Console.Services.Networking;
using Sietch_Console.Services.Profiles;
using Sietch_Console.ViewModels;
using Sietch_Console.ViewModels.Steps;
using SietchConsole.Core.Interfaces;
using SietchConsole.Data.Database;
using SietchConsole.Data.Repositories;
using System.Windows;

namespace Sietch_Console;

public partial class App : Application
{
    private readonly IHost _host;

    public App()
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureLogging(logging =>
            {
                // Keep default providers; our InMemoryAppLogSink is added below via DI
                logging.SetMinimumLevel(LogLevel.Information);
            })
            .ConfigureServices(ConfigureServices)
            .Build();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Database
        var dbPath = DatabasePathProvider.GetDatabasePath();
        services.AddDbContext<SietchConsoleDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));
        services.AddScoped<DatabaseInitializerService>();

        // ── In-memory app log sink (#143) ──────────────────────────────────────
        // Registered as both IAppLogSink (for the ViewModel) and ILoggerProvider
        // (hooked into the host's logging pipeline).
        services.AddSingleton<InMemoryAppLogSink>();
        services.AddSingleton<IAppLogSink>(sp => sp.GetRequiredService<InMemoryAppLogSink>());
        services.AddSingleton<ILoggerProvider>(sp => sp.GetRequiredService<InMemoryAppLogSink>());

        // ── Active profile service (#139) ─────────────────────────────────────
        services.AddSingleton<IActiveProfileService, ActiveProfileService>();

        // Infrastructure services
        services.AddSingleton<ISystemReadinessService, SystemReadinessService>();
        services.AddSingleton<ISteamDetectionService, SteamDetectionService>();
        services.AddSingleton<IServerPackageService, ServerPackageService>();
        services.AddSingleton<ISetupScriptService, SetupScriptService>();
        services.AddSingleton<ISteamCmdService, SteamCmdService>();
        services.AddSingleton<IServerPackageInstaller, ServerPackageInstaller>();
        services.AddSingleton<InstallationOrchestrator>();
        services.AddSingleton<IServerProcessService, ServerProcessService>();
        services.AddSingleton<IBattlegroupControlService, BattlegroupControlService>();
        services.AddSingleton<IIniParserService, IniParserService>();
        services.AddSingleton<IConfigurationService, ConfigurationService>();
        services.AddSingleton<ILogFileService, LogFileService>();
        services.AddSingleton<ILogAnalysisService, LogAnalysisService>();
        services.AddSingleton<IBackupService, BackupService>();
        services.AddSingleton<INetworkingService, NetworkingService>();

        // Repositories
        services.AddScoped<IApplicationSettingsRepository, ApplicationSettingsRepository>();
        services.AddScoped<IBattlegroupProfileRepository, BattlegroupProfileRepository>();
        services.AddScoped<IDiagnosticsResultRepository, DiagnosticsResultRepository>();
        services.AddScoped<IBackupRecordRepository, BackupRecordRepository>();
        services.AddScoped<ISetupWizardStateRepository, SetupWizardStateRepository>();

        // ViewModels
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<RequirementsStepViewModel>();
        services.AddSingleton<SetupWizardViewModel>();
        services.AddSingleton<LogsViewModel>();
        services.AddSingleton<DiagnosticsViewModel>();
        services.AddSingleton<BackupsViewModel>();
        services.AddSingleton<NetworkingViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<AppLogsViewModel>();

        // Views
        services.AddSingleton<MainWindow>();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        await _host.StartAsync();

        // Wire up the in-memory log sink into the live logging pipeline
        var loggerFactory = _host.Services.GetRequiredService<ILoggerFactory>();
        var sink          = _host.Services.GetRequiredService<InMemoryAppLogSink>();
        loggerFactory.AddProvider(sink);

        using (var scope = _host.Services.CreateScope())
        {
            var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializerService>();
            await initializer.InitializeAsync();
        }

        // ── #139: ActiveProfileService must initialize first ─────────────────
        var activeProfileService = _host.Services.GetRequiredService<IActiveProfileService>();
        await activeProfileService.InitializeAsync();

        // Initialize ViewModels (all now use IActiveProfileService for the active profile)
        var dashboardVm = _host.Services.GetRequiredService<DashboardViewModel>();
        await dashboardVm.InitializeAsync();

        var logsVm = _host.Services.GetRequiredService<LogsViewModel>();
        await logsVm.InitializeAsync();

        var settingsVm = _host.Services.GetRequiredService<SettingsViewModel>();
        await settingsVm.InitializeAsync();

        var backupsVm = _host.Services.GetRequiredService<BackupsViewModel>();
        await backupsVm.InitializeAsync();

        var networkingVm = _host.Services.GetRequiredService<NetworkingViewModel>();
        await networkingVm.InitializeAsync();

        var wizardVm = _host.Services.GetRequiredService<SetupWizardViewModel>();
        await wizardVm.InitializeAsync();

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();

        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        await _host.StopAsync();
        _host.Dispose();
        base.OnExit(e);
    }
}
