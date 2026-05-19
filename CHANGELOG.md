# Changelog

All notable changes to Sietch Console are documented in this file.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
Sietch Console uses [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

### Added

- **SSH transport layer for VM communication (#177, #178)** — `ISshService` / `SshService` provides a persistent SSH + SFTP session to the Hyper-V VM via SSH.NET 2024.2.0; `ConnectAsync` authenticates with an ed25519 private key, maintains both `SshClient` and `SftpClient`; `ExecuteAsync` runs one-shot commands and returns a typed `SshCommandResult`; `StreamLinesAsync` exposes an `IAsyncEnumerable<string>` for live log tailing; `ReadRemoteFileAsync` / `WriteRemoteFileAsync` provide SFTP file I/O; `TestConnectionAsync` opens a throw-away connection (no stored state) for connection checks in Settings
- **Pod-based battlegroup management via battlegroup binary (#179, #180)** — `PodMonitorService` replaces the previous process-based `ServerProcessService` for Kubernetes-native battlegroup lifecycle; `StartAsync` issues `battlegroup start` inside the VM over SSH then polls `battlegroup status` every 10 s; `StopAsync` issues `battlegroup stop`; log lines are streamed via `battlegroup logs`; `ServerStarted` fires when status reports healthy; `ProcessExited` fires on detected crash/failure; all commands route through `/home/dune/.dune/bin/battlegroup` (the official Funcom management binary, always at this path after `initial-setup.ps1`)
- **VM connection settings in Settings view** — new "VM Connection" card exposes VM IP address, SSH username (default `dune`), SSH port (default `22`), and SSH private key path (leave blank to use the default `%LOCALAPPDATA%\DuneAwakeningServer\sshKey` written by Funcom's initial-setup); Test Connection button verifies key file existence and SSH reachability before committing; Save VM Settings persists to the active battlegroup profile; effective key path displayed as a read-only hint
- **`BattlegroupProfile.DefaultSshKeyPath`** — static property that resolves the SSH key path Funcom's `initial-setup.ps1` always writes to (`%LOCALAPPDATA%\DuneAwakeningServer\sshKey`); used by both `BattlegroupControlService` and the Settings VM connection form as the fallback when no custom path is configured
- **Raw-editor sync warning (#184)** — `ShowRawSyncWarning` flag and `DismissRawSyncWarningCommand` now wired up in `SettingsViewModel`; the banner (already present in the XAML) correctly appears when both the structured form and the raw editor have unsaved changes simultaneously

### Changed

- `BattlegroupControlService.StartAsync` no longer requires `VmSshKeyPath` to be explicitly set on the profile — falls back to `BattlegroupProfile.DefaultSshKeyPath` automatically; opens SSH connection after VM reaches `Running` state, then delegates to `PodMonitorService` which issues `battlegroup start`
- `BattlegroupControlService.OpenControlInterface` now queries the VM's Battlegroup Director NodePort via `kubectl get svc` (port 11717) over SSH and opens `http://{ip}:{port}/`; falls back to the file browser at port 18888 when SSH is not connected or the query fails
- `BattlegroupControlService.OpenFileBrowser` now opens `http://{ip}:18888/` in the default browser (the in-VM file browser served by the battlegroup) instead of opening Windows Explorer
- `BattlegroupProfile.VmName` defaults to `"dune-awakening"` (the VM name hardcoded in Funcom's `battlegroup.ps1`)
- `SettingsViewModel` injects `ISshService` for connection testing; adds VM connection observable properties (`VmIpAddress`, `VmSshKeyPath`, `VmUsername`, `VmSshPort`, `VmSshStatusMessage`, `VmSshIsBusy`) and reloads them on profile change
- Required game port range updated to **7777–7810 UDP** and **31982 TCP** (RabbitMQ) per Funcom's official self-hosted server documentation

- **Cloud backup sync (#173, #174, #175, #176)** — `ICloudSyncService` / `CloudSyncService` zips a local backup directory and uploads it to a configured cloud provider; downloads are unzipped and handed to `IBackupService.RestoreAsync`; two concrete `ICloudStorageProvider` implementations ship: **OneDrive** (MSAL `PublicClientApplication`, interactive browser auth, DPAPI token cache, raw Microsoft Graph REST calls with 10 MB chunked upload) and **S3-compatible** (`AWSSDK.S3 v3`, `BasicAWSCredentials`, `ForcePathStyle` for non-AWS providers such as Backblaze and MinIO); `CloudSyncService.EncryptSecretKey` / `DecryptSecretKey` use `ProtectedData` (DPAPI) so S3 credentials are never stored in plain text
- **`CloudBackupFile` model** — lightweight record (`RemoteId`, `FileName`, `SizeBytes`, `CreatedAt`) returned by `ICloudStorageProvider.ListAsync`; displayed in the Cloud Backups list card
- **`BackupRecord.CloudSyncedAt` / `CloudRemoteId` fields** — track when a local backup was last pushed to the cloud and store its provider-assigned ID for future deletion
- **Cloud sync UI in Backups view** — provider selector (None / OneDrive / S3); enable toggle; remote folder path; OneDrive info banner; S3 fields (endpoint URL, bucket, region, access key ID, secret key via `PasswordBox`); Test Connection and Save buttons; Cloud Backups list with ↓ Restore and Delete actions; "↑ Sync" button and "☁ Synced" badge on each local backup row
- **Discord webhook integration (#170, #171, #172)** — `IDiscordWebhookService` / `DiscordWebhookService` POSTs colour-coded embeds to a Discord webhook URL on server start, stop, and crash events; fire-and-forget with one retry on HTTP 5xx; per-event toggles for start / stop / crash notifications; manual announcement panel on the Dashboard sends a free-text message to the configured channel; Test button in Settings verifies the URL without touching the enable flag
- **`IServerProcessService.ServerStarted` event** — fires once when the server-ready log pattern is matched in `ServerProcessService`; consumed by `DiscordWebhookService` for lifecycle notifications
- **Discord settings card in Settings view** — webhook URL field, enable toggle, per-event checkboxes (Server Online, Server Offline, Server Crash), Test and Save actions; status message shows result of the last test or save
- **Discord announcement card in Dashboard view** — text field and Send button; posts announcement as a blue embed to the configured webhook

### Changed

- `BackupsViewModel` injects `ICloudSyncService`; exposes cloud provider, credential, and status observable properties; adds `SaveCloudSettingsAsync`, `TestCloudConnectionAsync`, `SyncToCloudAsync`, `RefreshCloudBackupsAsync`, `DownloadAndRestoreCloudBackupAsync`, and `DeleteCloudBackupAsync` relay commands
- `ApplicationSettings` extended with `CloudSyncEnabled`, `CloudSyncProvider`, `CloudSyncFolderPath`, `S3BucketName`, `S3Region`, `S3EndpointUrl`, `S3AccessKeyId`, `S3EncryptedSecretKey`; existing databases upgraded automatically via `AddColumnIfMissingAsync` in `DatabaseInitializerService`
- `BackupRecordRepository` implements the new `UpdateAsync` method required to persist `CloudSyncedAt` / `CloudRemoteId` after a successful upload
- WPF project adds `AWSSDK.S3 3.7.502` and `Microsoft.Identity.Client 4.84.0` NuGet packages

---

## [0.6.0-alpha.1] — 2026-05-10

### Added

- **Multi-host support (#153, #154, #155, #156)** — `IHyperVHostService` / `HyperVHostService` manages a registry of remote Hyper-V hosts alongside the always-present local "This machine" entry; remote hosts are added by hostname, port, and optional WMI credentials; DPAPI (`ProtectedData.Protect`, `CurrentUser` scope) encrypts passwords before writing to SQLite; `TestConnectionAsync` probes the remote WMI namespace (`root\virtualization\v2`) via `ManagementScope` with `PacketPrivacy` authentication; `SwitchToAsync` sets the active host and fires `ActiveHostChanged`; `BattlegroupProfile.HostId` property links profiles to hosts (null = local)
- **`HyperVHost` model and repository** — `HyperVHost` record persisted to a new `HyperVHosts` SQLite table; `HyperVHostRepository` ordered local-first, then alpha by name
- **Player management — ban and allowlist (#158, #159, #160)** — `IPlayerManagementService` / `PlayerManagementService` parses `IServerProcessService.OutputLineReceived` with join/leave regex to maintain a live `ConnectedPlayers` list; `KickPlayerAsync` and `BanPlayerAsync` send console commands via the new `IServerProcessService.SendCommandAsync`; bans are persisted to `BanRecords` (SQLite); allowlist entries stored in `AllowlistEntries`; `UnbanPlayerAsync` / `RemoveFromAllowlistAsync` delete records by ID
- **Players view (#158–#160)** — `PlayersViewModel` + `PlayersView.xaml` with three tabs: Connected Players (live list, kick/ban actions), Ban List (persistent records, unban), Allowlist (add by Steam ID, remove); confirmation overlay for destructive actions; ban form overlay with permanent vs. timed options
- **Server metrics dashboard (#163, #164)** — `IMetricsCollectorService` / `MetricsCollectorService` fires a 60-second `PeriodicTimer`, samples `IBattlegroupControlService.GetVmResourcesAsync` for CPU % and memory MB, tracks uptime since server start, and writes `ServerMetricSnapshot` records via `IMetricsRepository`; old snapshots pruned at 30 days; downtime events opened on `ProcessExited` and closed when the server restarts
- **Metrics view (#163, #164)** — `MetricsViewModel` + `MetricsView.xaml`; time-range selector (1 h / 6 h / 24 h / 7 d / 30 d); three OxyPlot 2.x `LineSeries` charts (player count, CPU %, memory MB); availability percentage and uptime summary; downtime event list with reason, start time, and duration; live "Collecting" indicator; charts auto-append new snapshots without a full reload
- **`OxyPlot.Wpf` 2.1.2** added to the WPF project for charting
- **`IServerProcessService.SendCommandAsync`** — new interface method writes a line to the server process stdin; implemented in `ServerProcessService`; used by `PlayerManagementService` for kick/ban commands

### Changed

- `SietchConsoleDbContext` gains five new `DbSet<T>` entries: `HyperVHosts`, `BanRecords`, `AllowlistEntries`, `MetricSnapshots`, `DowntimeEvents`
- `DatabaseInitializerService` gains `CreateTableIfMissingAsync` helper for idempotent `CREATE TABLE IF NOT EXISTS` (used for the five new tables and the `BattlegroupProfiles.HostId` column) so existing user databases are upgraded automatically on next launch
- `App.xaml.cs` registers `IHyperVHostService`, `IPlayerManagementService`, `IMetricsCollectorService`, `IHyperVHostRepository`, `IMetricsRepository`, `PlayersViewModel`, `MetricsViewModel`; calls `IHyperVHostService.InitializeAsync()` on startup
- `MainWindowViewModel` adds **Players** and **Metrics** to the sidebar navigation
- `App.xaml` adds `DataTemplate` routes for `PlayersViewModel` → `PlayersView` and `MetricsViewModel` → `MetricsView`

---

## [0.5.0-alpha.1] — 2026-05-10

### Added

- **Embedded remote management web server (#152)** — `IRemoteManagementService` / `RemoteManagementService` wraps an ASP.NET Core Kestrel web host that starts inside the existing WPF `IHost`; configures to listen on `0.0.0.0:{port}` (default 5151) so LAN devices can reach it; shuts down cleanly with the app; auto-starts on launch when previously enabled
- **Remote management authentication (#151)** — `RemoteAuthMiddleware` enforces `Authorization: Bearer <token>` on all `/api/*` routes; SSE endpoint also accepts `?token=` query param because `EventSource` cannot send headers; IP-based rate limiting: 5 failures within 60 s blocks for 5 minutes with `429 Too Many Requests` and `Retry-After`; auth failures logged with source IP
- **REST and SSE API (#149)** — `RemoteApiEndpoints` maps minimal APIs: `GET /api/status` (status, uptimeSeconds, playerCount), `POST /api/control/{start,stop,restart}` (202 Accepted, dispatches to `IBattlegroupControlService`), `GET /api/logs?lines=50` (last N lines from the most recent log file), `GET /api/events` (SSE stream that pushes `status` and `log` events in real time); `SseHub` broadcasts to all connected clients via per-client `Channel<string>`
- **Remote dashboard web UI (#150)** — single-page dashboard compiled into the binary as `EmbeddedResource`; served at `GET /`; features status badge, uptime, player count, Start/Stop/Restart controls, scrolling 200-line log tail, live SSE updates; dark theme matching WPF palette (`#0B0F14` bg, `#C46A2B` accent); mobile-responsive layout; login screen stores token in `sessionStorage` (cleared on tab close)
- **Remote management settings in Settings view** — Remote Management card: enable toggle, port field, token field with one-click Generate button (`RandomNumberGenerator`, base64), live status indicator showing URL when running, Apply button; settings persisted to `ApplicationSettings` (`RemoteManagementEnabled`, `RemoteManagementPort`, `RemoteManagementToken`); web server starts/stops immediately when settings are saved
- **`docs/remote-api.md`** — full API reference for community integrators (all endpoints, SSE event shapes, auth model, curl examples)

### Changed

- `SettingsViewModel` injects `IRemoteManagementService` and exposes remote management observable properties and commands; subscribes to `IRemoteManagementService.StatusChanged` to keep the UI indicator in sync
- `App.xaml.cs` registers `IRemoteManagementService` / `RemoteManagementService` as singleton; auto-starts on launch when `RemoteManagementEnabled = true` in `ApplicationSettings`; stops the web server cleanly in `OnExit`
- `ApplicationSettings` extended with `RemoteManagementEnabled`, `RemoteManagementPort`, `RemoteManagementToken`; existing databases upgraded via `ALTER TABLE ADD COLUMN` in `DatabaseInitializerService`
- WPF project file adds `<FrameworkReference Include="Microsoft.AspNetCore.App" />` and embeds `Resources/dashboard.html`

---

## [0.4.0-alpha.1] — 2026-05-10

### Added

- **Multiple battleground profiles — create, switch, delete (#139)** — `IActiveProfileService` / `ActiveProfileService` singleton owns the active profile, persists the selection to `ApplicationSettings.LastOpenedBattlegroupId`, and fires `ProfileChanged` on the UI thread; all ViewModels (Dashboard, Logs, Settings, Backups, Networking) react instantly when the user switches; sidebar profile switcher (ComboBox + New + Delete) added to `MainWindow.xaml`; new-profile creation overlay (name + install path)
- **In-app application log viewer (#143)** — `InMemoryAppLogSink` implements both `ILoggerProvider` (receives all `Microsoft.Extensions.Logging` events) and `IAppLogSink` (exposes a ring-buffer of up to 500 entries); `AppLogsViewModel` with level and keyword filter; `AppLogsView.xaml` with level badge, category, message, and timestamp columns; registered as "App Logs" nav item
- **VM IP auto-detection save-back (#140)** — `NetworkingViewModel` now persists the detected `VmIp` back to `BattlegroupProfile.VmIpAddress` via the profile repository when the value changes
- **Auto-update from GitHub Releases (#145–#148)** — `AppUpdateService` queries the GitHub Releases API on startup, compares tag name with the running `InformationalVersion` (numeric + pre-release label), and sets `MainWindowViewModel.PendingUpdate` when a new version is available; update banner appears at the top of all views; one-click download streams the installer binary to `%TEMP%\SietchConsole_Update\` with progress reporting; installer is launched and the app shuts down automatically
- **Dynamic version string** — `MainWindowViewModel.AppVersion` reads `AssemblyInformationalVersionAttribute` at runtime, replacing the formerly hardcoded `v0.1.0-alpha.4`

### Changed

- All ViewModels now inject `IActiveProfileService` instead of manually querying `ApplicationSettingsRepository.LastOpenedBattlegroupId`; `InitializeAsync` is significantly simplified
- `MainWindowViewModel` now accepts `AppLogsViewModel` and `IAppUpdateService` via DI and includes all profile-management state

---

## [0.3.0-alpha.1] — 2026-05-10

### Added

- **Full backup (#135)** — `CreateFullBackupAsync` creates a `{stamp}_Full/` directory containing a `Config/` subdirectory (INI files) and a `SaveData/` subdirectory (full directory tree); `RestoreAsync` reads the manifest's `ConfigDirectory` and `SaveDirectory` fields to restore each part to its original location
- **Automatic backup scheduling (#138)** — `DispatcherTimer` fires at the configured interval (1/3/6/12/24 h) and calls `CreateFullBackupAsync`; the interval and the enabled flag are persisted via `IApplicationSettingsRepository`; auto-backup settings card (enable toggle, interval ComboBox, retention count, Save button) added to `BackupsView.xaml`
- **Backup retention pruning (#136)** — `PruneOldBackupsAsync` removes the oldest backups beyond `BackupRetainCount` after every manual or scheduled backup; pruning is also called from `BackupsViewModel.RunBackupAsync`
- **Setup Wizard resume (#141)** — `HasResumeOffer`, `ResumeCommand`, and `StartFreshCommand` already fully implemented in `SetupWizardViewModel`; `SetupWizardView.xaml` shows the resume-offer overlay automatically on re-open

### Changed

- `BackupsViewModel` now includes `AutoBackupEnabled`, `BackupIntervalHours`, `BackupRetainCount`, `IntervalChoices`, `SaveAutoBackupSettingsCommand`, and `BackupFullCommand`
- `ApplicationSettings` extended with `AutoBackupEnabled`, `BackupIntervalHours`, `BackupRetainCount`; existing databases updated via `ALTER TABLE ADD COLUMN` in `DatabaseInitializerService`
- `BackupManifest` (private) extended with `ConfigDirectory` and `SaveDirectory` fields; `CreateSaveDataBackupAsync` now sets `SaveDirectory`

---

## [0.2.0-alpha.1] — 2026-05-10

### Added

- **Live server process management (Milestone 17)** — `ServerProcessService` spawns the Dune: Awakening dedicated server executable directly from `InstallPath` on the host machine
- **Live stdout/stderr streaming to Logs view (#129)** — every line written by the server process is parsed and added to the log buffer in real time; the Logs header shows a "● LIVE" pill badge while streaming
- **Server-ready detection (#130)** — `ServerProcessService` scans output for UE5 listener patterns (e.g. "listening on port", "accepting connections") and transitions the Dashboard to Running once detected; the 10-second status timer picks this up automatically
- **Crash and exit-code surfacing (#131)** — `ProcessExited` event fires with a human-readable description for common Windows crash codes (0xC0000005 access violation, 0xC0000FD stack overflow, etc.); unexpected exits set the Dashboard to Error immediately via a Dispatcher callback
- **Graceful shutdown sequence (#132)** — `StopAsync` writes "quit" to stdin and sends WM_CLOSE before falling back to force-kill; `BattlegroupControlService.StopAsync` stops the server process first (10 s window) before issuing the Hyper-V ACPI shutdown
- **`IServerProcessService` interface** — clean Core-layer contract for process lifecycle; both `BattlegroupControlService` and the two ViewModels depend on it via DI

### Changed

- **`BattlegroupControlService.StartAsync`** — no longer requires a VM name; if `VmName` is set it provisions and starts the Hyper-V VM first, then always calls `ServerProcessService.StartAsync` to launch the server process on the host
- **`BattlegroupControlService.GetStatusAsync`** — live process state now takes priority: `IsRunning + IsServerReady → Running`, `IsRunning + !IsServerReady → Starting`; VM/host-process fallback still applies when the process has not been started
- **`BattlegroupControlService.StopAsync`** — no longer throws when `VmName` is empty; exits cleanly after stopping the process if there is no VM to shut down
- **`LogsViewModel`** — now accepts `IServerProcessService` via constructor injection; subscribes to `OutputLineReceived` and `ProcessExited` events and exposes `IsLiveStreaming` and `StreamStatusLabel`
- **`DashboardViewModel`** — now accepts `IServerProcessService` via constructor injection; subscribes to `ProcessExited` and immediately surfaces unexpected exits as Error state with a descriptive message

---

## [0.1.0-alpha.4] — 2026-05-10

### Added

- **Real Hyper-V integration (Milestone 15)** — `BattlegroupControlService` now issues actual WMI queries and PowerShell commands instead of stubs
- **VM provisioning** — if the configured VM does not exist, `Start` automatically creates it (`New-VM`, `Set-VMProcessor`, `Set-VMMemory`) using the profile's CPU count, RAM, and virtual switch
- **Graceful stop + force fallback** — `Stop` sends an ACPI shutdown signal, waits 30 s, then falls back to `Stop-VM -Force`
- **State-transition polling** — Start waits up to 90 s for the VM to reach Running; all wait loops poll WMI every 2 s
- **VM resource utilisation on Dashboard** — CPU% and RAM displayed live via `Msvm_SummaryInformation` while the VM is Running
- **Typed Hyper-V exceptions** — `HyperVException` with `HyperVErrorCode` (AccessDenied, VmNotFound, OperationTimedOut, …) surfaces user-readable messages
- **Dashboard error banner** — Hyper-V failures surface as an amber dismissible banner above the status card instead of silently failing
- **Automatic SQLite schema migration** — `DatabaseInitializerService` runs `ALTER TABLE ADD COLUMN` for the three new profile fields (`CpuCount`, `MemoryMb`, `VirtualSwitchName`) so existing databases upgrade on next launch

### Security

- **PowerShell `-EncodedCommand`** — all `powershell.exe` invocations now pass the full script as Base64 UTF-16 via `-EncodedCommand`, eliminating any shell-quoting injection surface in VM names and paths

---

## [0.1.0-alpha.3] — 2026-05-10

### Added

- **Toast notification overlay** — all async operations (save, backup, restore, firewall) now surface success, warning, and error toasts via a central `NotificationService`; success/warning auto-dismiss after 5 s, errors persist until dismissed
- **Install path validation in Setup Wizard** — inline error messages flag drive roots and system-protected directories before the user can advance
- **Token validation in Setup Wizard** — account token is auto-trimmed of whitespace, capped at 2048 chars, and flagged with a hint if it looks too short
- **Adapter picker in Networking view** — when the host has more than one network adapter, a dropdown lets the user choose which IP is advertised to players
- **Log file auto-refresh** — a `FileSystemWatcher` detects new `.log` files in the log directory and refreshes the file list without a manual page reload
- **"Copy Logs" toolbar button** — copies the currently filtered log lines to the clipboard
- **Progress step cancel + timeout** — the installation step now exposes a Cancel button; a 30-minute `CancellationToken` auto-cancels and surfaces a timeout banner
- **Raw editor sync warning** — Settings warns when both structured form fields and the raw INI editor have unsaved changes, with an actionable Dismiss button

### Changed

- **`IActiveProfileService` singleton** — the active battlegroup profile is resolved once and cached for the session, eliminating a redundant DB query on every view navigation
- **Dashboard shortcuts conditional** — "Open File Browser" is disabled when the install path does not exist on disk
- **UAC cancel feedback in Networking** — cancelling the firewall UAC prompt now shows an explicit retry message instead of leaving the button in a silent failed state
- **Restore progress message** — Backups view now shows "Restoring backup…" as intermediate state before the final success/failure notification

### Security

- **DPAPI encryption at rest** — the Dune account token and admin password stored in the local SQLite database are now encrypted with Windows Data Protection API (`DataProtectionScope.CurrentUser`) via `SecureStorageService`

### Performance

- `LogsViewModel` internal buffer changed from `List<LogEntry>` to `Queue<LogEntry>` — cap eviction is now O(1) instead of O(n); `DispatcherTimer` and `FileSystemWatcher` are properly disposed via `IDisposable`

---

## [0.1.0-alpha.2] — 2026-05-09

### Added

**Brand identity**
- Full brand asset integration from Claude Design: multi-resolution app icon (`AppIcon.ico`, 16–256 px), cinematic splash screen (`Splash.png`), and `BrandResources.xaml` containing logo mark geometry, 15 vector nav icons, and the `AppLockupTemplate` lockup
- `ResourceKeyToGeometryConverter` — runtime resource-key-to-geometry lookup for data-bound icon paths

**Documentation**
- `README.md` — user- and developer-facing project overview with feature table, requirements, quick start, and architecture summary
- `CLAUDE.md` — AI development guidance document (auto-loaded by Claude Code)
- `CONTRIBUTING.md` — branch naming, PR process, and coding standards
- `docs/architecture.md` — layer diagram, service map, and data flow
- `docs/user-setup-guide.md` — beginner-friendly Setup Wizard walkthrough
- `docs/troubleshooting.md` — diagnostics interpretation and recovery workflows
- `docs/known-limitations.md` — alpha stubs, known bugs, and unsupported configurations
- `docs/roadmap.md` — Milestones 15–20 and post-MVP plans
- `docs/release-checklist.md` — pre-release QA checklist
- `INSTALL.md` — installation and first-launch instructions

**Repository**
- Repo made public; MIT license confirmed
- README badges: CI build, release pipeline, version, license, platform, .NET, GitHub Sponsors
- `.github/FUNDING.yml` — enables GitHub Sponsors button in repo UI

### Changed

- Header logo and wordmark replaced with `AppLockupTemplate` from `BrandResources.xaml` (removes ~120 lines of inline path geometry)
- Sidebar navigation icons swapped from Segoe MDL2 Assets glyphs to brand vector `Path` elements styled with `BrandIconStyle`
- `BaseButtonStyle` now sets `Padding="14,8"` — all button variants inherit consistent spacing app-wide
- `SietchTheme.xaml` updated with full brand-board token pass (new brushes, gradients, typography resources)
- Inno Setup `SetupIconFile` now points to `Resources\AppIcon.ico`

### Fixed

- Inno Setup `[Registry]` and `[Run]` entries were split across multiple lines, causing "Required parameter not specified" compile errors in CI — collapsed to single lines
- Release workflow was triggering on a stale tag; tag moved to current HEAD after each fix

---

## [0.1.0-alpha.1] — 2026-05-09

First internal alpha release. Core features are functional; the application is not yet ready for general public use.

### Added

**Application shell**
- WPF .NET 8 desktop application with MVVM architecture (CommunityToolkit.Mvvm)
- Sidebar navigation with Segoe MDL2 Assets icons
- Persistent profile state via local SQLite database (Microsoft.Extensions.Hosting DI)
- Global theme system: Deep Onyx palette, Spice Orange accents, Segoe UI Variable typography
- Geometric arch doorway logo mark with amber gradient

**Dashboard**
- Battlegroup status display (Running / Starting / Stopping / Error / Offline)
- Start, Stop, and Restart controls with confirmation overlay
- Access shortcuts (Control Interface, File Browser, VM Shell)
- Live status indicator in the window status bar

**Setup Wizard**
- Multi-step guided onboarding: Welcome → Requirements → Install Path → Token → VM Config → Battlegroup Config → Review → Progress
- System requirements validation with per-check status and remediation guidance
- Battlegroup profile saved to SQLite on completion

**System Diagnostics**
- On-demand system readiness checks (Hyper-V, virtualization, memory, disk, network)
- Per-result severity badges (Pass / Warning / Failure) with friendly explanations
- Expandable technical detail for advanced users

**Logs**
- Live log streaming from battlegroup process output
- Search and severity filtering (All / Error / Warning / Info)
- Detected issue panel with explanations and recommendations
- Auto-scroll toggle, log file selector, export and copy-report actions

**Configuration (Settings)**
- Structured editing of server identity, network, and gameplay multipliers
- Raw INI editor with file selector and revert support
- Automatic config backup created before every save

**Backup and Restore**
- On-demand configuration and save-data backups with timestamped records
- Restore and delete workflows with confirmation dialogs
- Backup listing with type badges (Config / Save Data / Full)

**Networking Assistant**
- Host and Hyper-V VM IP detection via `NetworkInterface` enumeration
- Required port table (game traffic, beacon, Steam query/relay)
- Windows Firewall inbound rule checker and UAC-elevated rule creator
- Port forwarding step-by-step guide
- Local port listening test (netstat-based)
- Copyable plain-text connection summary for sharing with players

**Release pipeline**
- `Directory.Build.props` — solution-wide versioning and assembly metadata
- GitHub Actions CI (`build.yml`) — build validation on push/PR
- GitHub Actions release (`release.yml`) — tag-triggered self-contained publish + Inno Setup installer + draft GitHub Release
- Inno Setup installer script (`Installer/sietch-console.iss`)

---

[Unreleased]: https://github.com/michaelstoffer/sietch-console/compare/v0.6.0-alpha.1...HEAD
[0.6.0-alpha.1]: https://github.com/michaelstoffer/sietch-console/compare/v0.5.0-alpha.1...v0.6.0-alpha.1
[0.5.0-alpha.1]: https://github.com/michaelstoffer/sietch-console/compare/v0.4.0-alpha.1...v0.5.0-alpha.1
[0.4.0-alpha.1]: https://github.com/michaelstoffer/sietch-console/compare/v0.3.0-alpha.1...v0.4.0-alpha.1
[0.3.0-alpha.1]: https://github.com/michaelstoffer/sietch-console/compare/v0.2.0-alpha.1...v0.3.0-alpha.1
[0.2.0-alpha.1]: https://github.com/michaelstoffer/sietch-console/compare/v0.1.0-alpha.4...v0.2.0-alpha.1
[0.1.0-alpha.4]: https://github.com/michaelstoffer/sietch-console/compare/v0.1.0-alpha.3...v0.1.0-alpha.4
[0.1.0-alpha.3]: https://github.com/michaelstoffer/sietch-console/compare/v0.1.0-alpha.2...v0.1.0-alpha.3
[0.1.0-alpha.2]: https://github.com/michaelstoffer/sietch-console/releases/tag/v0.1.0-alpha.2
[0.1.0-alpha.1]: https://github.com/michaelstoffer/sietch-console/releases/tag/v0.1.0-alpha.1
