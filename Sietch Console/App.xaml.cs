using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sietch_Console.Services.Diagnostics;
using Sietch_Console.Services.Installation;
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

        // Infrastructure services
        services.AddSingleton<ISystemReadinessService, SystemReadinessService>();
        services.AddSingleton<ISteamDetectionService, SteamDetectionService>();
        services.AddSingleton<IServerPackageService, ServerPackageService>();
        services.AddSingleton<ISetupScriptService, SetupScriptService>();
        services.AddSingleton<InstallationOrchestrator>();

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

        // Views
        services.AddSingleton<MainWindow>();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        await _host.StartAsync();

        using (var scope = _host.Services.CreateScope())
        {
            var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializerService>();
            await initializer.InitializeAsync();
        }

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
