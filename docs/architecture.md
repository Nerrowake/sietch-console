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
| `VmResourceSnapshot` | Point-in-time CPU% and RAM reading from `Msvm_SummaryInformation` |
| `ServerProcessExitEventArgs` | Exit code, human-readable description, and `WasExpected` flag from a server process exit |
| `AppLogEntry` | A single in-memory application log entry: timestamp, level, category, message, optional exception |
| `AppUpdateInfo` | Metadata from a GitHub Release: tag name, release name, HTML URL, installer download URL, release notes |

### Interfaces (`Core/Interfaces/`)

Service contracts that the WPF app depends on, with implementations in the app project:

| Interface | Implemented by |
|-----------|---------------|
| `ISystemReadinessService` | `SystemReadinessService` |
| `IBattlegroupControlService` | `BattlegroupControlService` |
| `IServerProcessService` | `ServerProcessService` |
| `IConfigurationService` | `ConfigurationService` |
| `IIniParserService` | `IniParserService` |
| `ILogFileService` | `LogFileService` |
| `ILogAnalysisService` | `LogAnalysisService` |
| `IBackupService` | `BackupService` |
| `INetworkingService` | `NetworkingService` |
| `ISteamDetectionService` | `SteamDetectionService` |
| `IServerPackageService` | `ServerPackageService` |
| `ISteamCmdService` | `SteamCmdService` |
| `IServerPackageInstaller` | `ServerPackageInstaller` |
| `ISetupScriptService` | `SetupScriptService` |
| `IActiveProfileService` | `ActiveProfileService` — owns active profile, fires `ProfileChanged` event |
| `IAppLogSink` | `InMemoryAppLogSink` — ring-buffer log sink for the App Logs view |
| `IAppUpdateService` | `AppUpdateService` — GitHub Releases update check and installer download |
| `IRemoteManagementService` | `RemoteManagementService` — embedded Kestrel web server lifecycle |

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
- **Schema evolution:** `DatabaseInitializerService.InitializeAsync()` calls `EnsureCreated` (creates schema on first run) then `ApplySchemaUpdatesAsync`, which runs `ALTER TABLE … ADD COLUMN` statements for any columns added after initial release. This lets existing user databases upgrade automatically on next launch without EF Core migrations.
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
| Setup Wizard | `SetupWizardViewModel` |
| Logs | `LogsViewModel` |
| Diagnostics | `DiagnosticsViewModel` |
| Backups | `BackupsViewModel` |
| Networking | `NetworkingViewModel` |
| Settings | `SettingsViewModel` |
| App Logs | `AppLogsViewModel` |

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
│                   ServerProcessService — spawn/monitor/stop the server exe
├── Configuration/  ConfigurationService, IniParserService
├── Diagnostics/    SystemReadinessService + individual Check classes
├── Logs/           LogFileService, LogAnalysisService, InMemoryAppLogSink
├── Backups/        BackupService
├── Networking/     NetworkingService
├── Profiles/       ActiveProfileService — active profile ownership + ProfileChanged event
├── Update/         AppUpdateService — GitHub Releases update check and installer download
├── Remote/         RemoteManagementService — embedded Kestrel web server lifecycle
│                   RemoteApiEndpoints — minimal API route registration
│                   RemoteAuthMiddleware — Bearer token auth + IP rate limiting
│                   SseHub — per-client Channel<string> SSE broadcast hub
└── Installation/   SteamDetectionService, SteamCmdService, ServerPackageService,
                    ServerPackageInstaller, SetupScriptService, InstallationOrchestrator
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

## Hyper-V Integration

`BattlegroupControlService` performs real Hyper-V operations via WMI (`System.Management`) and PowerShell:

- **VM state queries** — `Msvm_ComputerSystem.EnabledState` via `root\virtualization\v2`
- **Start / Stop / Restart** — `Start-VM` / `Stop-VM` PowerShell cmdlets, invoked via `-EncodedCommand` (Base64 UTF-16) to eliminate all shell-quoting issues
- **VM provisioning** — `New-VM`, `Set-VMProcessor`, `Set-VMMemory` when the configured VM does not yet exist
- **Graceful stop** — ACPI shutdown signal first, then `Stop-VM -Force` if the guest does not shut down within 30 s
- **State-transition polling** — WMI polled every 2 s until the target state is reached or a timeout fires (90 s for Start)
- **Resource utilisation** — CPU% and RAM read from `Msvm_SummaryInformation.ProcessorLoad` / `MemoryUsage` while the VM is Running
- **Typed errors** — `HyperVException` with `HyperVErrorCode` maps WMI and PowerShell failures to user-readable messages

Server package management and server process management are fully implemented as of `0.2.0-alpha.1`.

---

## SteamCMD and Server Package Management

`SteamCmdService` manages the SteamCMD binary lifecycle:

- **Discovery** — searches `%LOCALAPPDATA%\SietchConsole\steamcmd\`, then `C:\steamcmd\`, then the Steam client directory
- **Auto-download** — if not found, downloads `steamcmd.zip` from Valve's CDN, extracts it, and runs `+quit` to self-initialize
- **Command execution** — spawns SteamCMD as a child process with redirected stdout/stderr and streams output via a callback

`ServerPackageInstaller` handles server file operations:

- **Install / Update** — `+login anonymous +force_install_dir ... +app_update {appid} validate +quit`; SteamCMD is idempotent so both operations use the same command
- **Integrity verification** — checks for known server executables (`DedicatedServer.exe`, `DuneServer.exe`)
- **Update check** — reads the installed build ID from `steamapps/appmanifest_{appid}.acf` and compares it against the current build ID from `+app_info_print`; returns `bool?` (true = update available, false = up to date, null = indeterminate)
- **Build ID persistence** — the installed build ID is written to `ApplicationSettings.InstalledBuildId` in SQLite after each install or update

---

## Server Process Integration

`ServerProcessService` manages the dedicated server process on the host machine:

- **Executable discovery** — searches `InstallPath` and common subdirectories (`Binaries\Win64`, `bin`, etc.) for known exe names
- **Process spawn** — `System.Diagnostics.Process` with `RedirectStandardOutput`, `RedirectStandardError`, and `RedirectStandardInput`; `CreateNoWindow = true`
- **Live output streaming** — `OutputDataReceived` / `ErrorDataReceived` callbacks fire `OutputLineReceived` events; `LogsViewModel` subscribes and routes each line through `ILogFileService.ParseLine` into its buffer, displaying a `● LIVE` badge while streaming
- **Server-ready detection** — regex patterns scan stdout for UE5 listener phrases (e.g. `"listening on port"`, `"accepting connections"`); once matched, `IsServerReady = true` and `BattlegroupControlService.GetStatusAsync` returns `Running`
- **Exit handling** — `ProcessExited` fires with `WasExpected` (true for explicit stops) and a description mapping common Windows crash codes (0xC0000005, 0xC0000FD, etc.) to plain English; `DashboardViewModel` subscribes and immediately updates the Dashboard to Error state on unexpected exits
- **Graceful shutdown** — writes `"quit"` to stdin → `CloseMainWindow()` → waits the timeout → `Kill(entireProcessTree: true)`

`BattlegroupControlService` coordinates the two layers:

- `GetStatusAsync` — checks live process state (`IsRunning`, `IsServerReady`) first; falls back to VM WMI queries and host-process enumeration
- `StartAsync` — starts the Hyper-V VM (if configured), then calls `ServerProcessService.StartAsync`
- `StopAsync` — calls `ServerProcessService.StopAsync` (10 s graceful window) before issuing the ACPI VM shutdown

---

## Key Dependencies

| Package | Purpose |
|---------|---------|
| `CommunityToolkit.Mvvm` | Source-generated MVVM base types |
| `Microsoft.Extensions.Hosting` | DI container and application lifetime |
| `Microsoft.EntityFrameworkCore.Sqlite` | SQLite persistence |
| `Microsoft.EntityFrameworkCore.Tools` | EF migrations |
