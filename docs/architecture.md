# Architecture

This document describes the internal structure, design decisions, and data flow of Sietch Console.

---

## Overview

Sietch Console is a Windows-native desktop application built with WPF and .NET 8. It follows an MVVM pattern with `Microsoft.Extensions.Hosting` providing dependency injection and application lifetime management.

The solution is divided into three projects with a strict layering rule:

```
┌─────────────────────────────────────────────┐
│          Sietch Console (WPF app)           │
│  Views · ViewModels · Services · Theme      │
│                                             │
│  References Core + Data                     │
└────────────────────┬────────────────────────┘
                     │
         ┌───────────┴────────────┐
         │                        │
┌────────▼──────────┐  ┌──────────▼─────────┐
│ SietchConsole.Data│  │ SietchConsole.Core  │
│ EF Core + SQLite  │  │ Models + Interfaces │
│ Repositories      │  │ No dependencies     │
│ References Core   │  └────────────────────┘
└───────────────────┘
```

**Core** has zero project references — it is a pure domain layer of sealed records and service interfaces. **Data** implements the repository interfaces from Core against a SQLite database via EF Core. The WPF app holds all application logic and UI.

---

## Project: SietchConsole.Core

The domain layer. It defines what the application knows about, not how it does it.

### Models (`Core/Models/`)

Sealed records representing domain objects. They carry no behavior.

| Type | Purpose |
|------|---------|
| `BattlegroupProfile` | The saved server configuration: name, install path, token, ports, VM settings |
| `BattlegroupRuntimeStatus` | Live state snapshot: `Running`, `Starting`, `Stopping`, `Error`, `Offline` |
| `BattlegroupConfig` | Parsed server INI settings (identity, gameplay multipliers, network) |
| `SetupWizardState` | Persisted progress through the Setup Wizard |
| `ApplicationSettings` | User preferences stored in SQLite |
| `DiagnosticsResult` | A single check result: name, severity, description, detail |
| `BackupRecord` | Metadata for a saved backup: timestamp, type, path, size |
| `IniDocument` | Parsed representation of an INI file: sections and key-value pairs |
| `LogEntry` | A single parsed log line: timestamp, severity, message |
| `DetectedIssue` | A pattern-matched issue found in log output |
| `NetworkInfo` | Host IP, VM IP, gateway, and detected interface name |
| `BattlegroupPort` | A named port: number, protocol, description, firewall status |
| `FirewallRuleStatus` | Whether a firewall rule exists and its current state |
| `ConnectivityResult` | Result of a local port-open test |
| `LogSeverity` | Enum: `Info`, `Warning`, `Error` |

### Interfaces (`Core/Interfaces/`)

Service contracts that the WPF app depends on, with implementations in the app project:

| Interface | Implemented by |
|-----------|---------------|
| `ISystemReadinessService` | `SystemReadinessService` |
| `IBattlegroupControlService` | `BattlegroupControlService` |
| `IConfigurationService` | `ConfigurationService` |
| `IIniParserService` | `IniParserService` |
| `ILogFileService` | `LogFileService` |
| `ILogAnalysisService` | `LogAnalysisService` |
| `IBackupService` | `BackupService` |
| `INetworkingService` | `NetworkingService` |
| `ISteamDetectionService` | `SteamDetectionService` |
| `IServerPackageService` | `ServerPackageService` |
| `ISetupScriptService` | `SetupScriptService` |

Repository interfaces (implemented in `SietchConsole.Data`):

| Interface | Purpose |
|-----------|---------|
| `IBattlegroupProfileRepository` | CRUD for the active battlegroup profile |
| `IApplicationSettingsRepository` | CRUD for user preferences |
| `ISetupWizardStateRepository` | Persist setup wizard progress across restarts |
| `IDiagnosticsResultRepository` | Store and retrieve diagnostic history |
| `IBackupRecordRepository` | Store and retrieve backup metadata |

---

## Project: SietchConsole.Data

The persistence layer. It implements repository interfaces using EF Core 8 against a SQLite database.

- **Database location:** `%LOCALAPPDATA%\SietchConsole\sietch.db`
- **Migrations:** In `Data/Migrations/` — run automatically at startup via `DbContext.Database.Migrate()`
- **Lifetime:** Repositories are registered as **scoped** services. ViewModels access them via `IServiceScopeFactory` to avoid holding an open connection for the lifetime of the application.

### Entities (`Data/Entities/`)

EF Core entity classes map 1:1 to Core models. They contain only persistence concerns (keys, column attributes) and have no domain logic.

---

## Project: Sietch Console (WPF app)

### Startup (`App.xaml.cs`)

`App` builds a `Microsoft.Extensions.Hosting` host with:

1. All service implementations registered (singletons unless otherwise noted)
2. All ViewModels registered as singletons
3. `SietchConsoleDbContext` registered via `AddDbContextFactory`
4. Repositories registered as scoped
5. `MainWindow` resolved from DI and shown

### Navigation

`MainWindowViewModel` owns a list of `NavigationItem` objects and an `ActiveView` property. `MainWindow.xaml` binds `ContentControl.Content` to `ActiveView`. Each view is resolved from DI once and cached — navigation is just a property change, not a new instance.

Navigation items and their target ViewModels:

| Label | ViewModel |
|-------|-----------|
| Dashboard | `DashboardViewModel` |
| Setup | `SetupWizardViewModel` |
| Logs | `LogsViewModel` |
| Diagnostics | `DiagnosticsViewModel` |
| Backups | `BackupsViewModel` |
| Networking | `NetworkingViewModel` |
| Settings | `SettingsViewModel` |

### Views (`Views/`)

One XAML file per navigation section, plus step views for the Setup Wizard under `Views/Steps/`. Views have minimal code-behind — only event handlers that cannot be expressed as commands (e.g., `ScrollViewer` auto-scroll, `TextBox` focus).

### ViewModels (`ViewModels/`)

All ViewModels extend `ObservableObject` from `CommunityToolkit.Mvvm`. Properties use `[ObservableProperty]` source generation; commands use `[RelayCommand]`. Setup wizard steps are in `ViewModels/Steps/`.

Standard ViewModel contract:

- `IsLoading` (`bool`) — bound to `LoadingBarStyle` ProgressBar
- Async command methods named `{Verb}{Noun}Async`
- No direct OS calls — always delegate to a service

### Services (`Services/`)

Application-layer implementations of the Core interfaces, organized by feature:

```
Services/
├── Control/        BattlegroupControlService — VM power, process management
├── Configuration/  ConfigurationService, IniParserService
├── Diagnostics/    SystemReadinessService + individual Check classes
├── Logs/           LogFileService, LogAnalysisService
├── Backups/        BackupService
├── Networking/     NetworkingService
└── Installation/   SteamDetectionService, ServerPackageService, SetupScriptService,
                    InstallationOrchestrator
```

Each check in `Services/Diagnostics/Checks/` implements a common check interface and is executed by `SystemReadinessService`. Adding a new diagnostic check means creating a new class in that folder and registering it.

### Theme (`Themes/SietchTheme.xaml`)

A single `ResourceDictionary` merged into `App.xaml`. It defines:

- Background brushes (8 levels of depth)
- Border brushes (3 variants)
- Accent brushes (6 colors)
- Text brushes (4 levels)
- Status brushes (Running, Offline, Error, Warning, etc.)
- Typography resources (font family, 6 size steps)
- Corner radii (Small, Medium, Large)
- All named styles (Button variants, Card, TextBlock roles, ProgressBar, DataGrid, etc.)
- Value converter instances

---

## Data Flow: Typical ViewModel Operation

```
User action
  → RelayCommand fires async method in ViewModel
  → ViewModel sets IsLoading = true
  → ViewModel calls service method (via injected interface)
  → Service performs OS/disk/network operation
  → Service returns result (model or exception)
  → ViewModel updates ObservableProperties
  → ViewModel sets IsLoading = false
  → View updates via data binding
```

For database reads/writes, the ViewModel opens a scope:

```csharp
using var scope = _scopeFactory.CreateScope();
var repo = scope.ServiceProvider.GetRequiredService<IBackupRecordRepository>();
var records = await repo.GetAllAsync();
```

---

## Data Storage

| What | Where |
|------|-------|
| SQLite database | `%LOCALAPPDATA%\SietchConsole\sietch.db` |
| Configuration backups | `%LOCALAPPDATA%\SietchConsole\Backups\` |
| Log files (read-only) | Server install path, as configured in the battlegroup profile |

The application never writes to the directory it was installed into. All mutable state lives under `%LOCALAPPDATA%\SietchConsole\`.

---

## Hyper-V Integration (Alpha Status)

In the current alpha, VM control (`BattlegroupControlService`) and server package management (`ServerPackageService`, `SetupScriptService`) are stubs. They return placeholder state rather than making real Hyper-V or SteamCMD calls. The interfaces and method signatures are finalized; the implementations will be filled in as later milestones target Hyper-V integration.

---

## Key Dependencies

| Package | Purpose |
|---------|---------|
| `CommunityToolkit.Mvvm` | Source-generated MVVM base types |
| `Microsoft.Extensions.Hosting` | DI container and application lifetime |
| `Microsoft.EntityFrameworkCore.Sqlite` | SQLite persistence |
| `Microsoft.EntityFrameworkCore.Tools` | EF migrations |
