# Roadmap

This document describes the planned feature trajectory for Sietch Console. Items are grouped by milestone. Ordering and scope are subject to change as the project evolves.

The current release is `0.6.0-alpha.1`. Milestones 15–24, 26, and 28 are complete. Milestone 25 (Mod Management) was cancelled. Milestone 27 (Cloud Backup) is planned.

---

## Milestone 15 — Hyper-V Integration ✓ Complete (`0.1.0-alpha.4`)

Replaced the stub server control layer with real Hyper-V operations.

- Provision a new Hyper-V VM (generation, memory, CPU, virtual switch) from within the app
- Start, stop, and restart the VM using PowerShell cmdlets (`Start-VM`, `Stop-VM`)
- Graceful ACPI shutdown with a 30-second timeout before force-stopping
- Poll VM state every 2 s until the target state is reached (90-second timeout for Start)
- Display live CPU% and RAM on the Dashboard while the VM is Running
- `HyperVException` with `HyperVErrorCode` surfaces friendly messages for access denied, VM not found, timeouts, and WMI failures
- Automatic SQLite schema migration for new profile fields (`CpuCount`, `MemoryMb`, `VirtualSwitchName`)

---

## Milestone 16 — Server Package Management ✓ Complete (`0.2.0-alpha.1`)

Automate server file installation and updates.

- Detect SteamCMD on the host machine or download it automatically
- Download the Dune: Awakening dedicated server files via SteamCMD inside or outside the VM
- Verify installation integrity after download
- Check for server file updates and surface an "Update Available" indicator on the Dashboard
- One-click server update from the Dashboard (stop → update → restart)

---

## Milestone 17 — Live Server Integration ✓ Complete (`0.2.0-alpha.1`)

Connect the app to the running server process.

- Spawn and monitor the Dune: Awakening server process inside the VM
- Stream live stdout/stderr output to the Logs view
- Detect server-ready state from log output and transition Dashboard status to Running
- Capture the server process exit code and surface error states with explanations
- Support graceful shutdown (SIGTERM equivalent) vs. force stop

---

## Milestone 18 — Save Data Backups ✓ Complete (`0.3.0-alpha.1`)

Complete the backup system with save data support.

- Detect the Dune: Awakening save data directory path from the server configuration
- Implement save data backup: compress and archive to `%LOCALAPPDATA%\SietchConsole\Backups\`
- Implement save data restore: stop server, swap files, restart
- Full backup type: config + save data in one operation
- Scheduled automatic backups: configurable interval (1/3/6/12/24 h) with enable toggle
- Backup pruning: keep last N backups, auto-delete older records

---

## Milestone 19 — Polish and Hardening ✓ Complete (`0.4.0-alpha.1`)

Address quality-of-life gaps and reliability issues from alpha feedback.

- Application icon (`.ico`) and taskbar icon — proper Windows app identity
- Splash screen on startup while the host initializes
- Setup Wizard resume support — if interrupted mid-setup, resume from the last completed step
- Multiple battlegroup profiles — create, switch between, and delete named profiles
- Hyper-V network adapter auto-detection for the VM IP on the Networking tab
- Log file selector auto-refresh when new files appear
- In-app application log viewer (Sietch Console's own logs, not the game server's)
- Toast notification system for async operation results
- DPAPI encryption at rest for the Dune account token and admin password

> **Note:** Code-signed installer is deferred. It requires a purchased certificate and is tracked separately.

---

## Milestone 20 — Auto-Update ✓ Complete (`0.4.0-alpha.1`)

Keep Sietch Console itself up to date.

- Check GitHub Releases API for a newer version on startup
- Display an "Update Available" banner with release notes summary
- One-click download and install of the new version (installer variant) or portable ZIP extraction
- Silent update option: download in the background, prompt to restart when ready

---

## Milestone 21 — Remote Management ✓ Complete (`0.5.0-alpha.1`)

Monitor and control the server from any device on the local network.

- Embedded Kestrel web server that starts inside the WPF app and listens on a configurable LAN port (default 5151)
- REST API: `GET /api/status`, `POST /api/control/{start,stop,restart}`, `GET /api/logs`, `GET /api/events` (SSE)
- Bearer token authentication with IP-based rate limiting (5 failures → 5-minute block)
- Built-in single-page dashboard: status badge, uptime, controls, live log tail — dark-themed, mobile-responsive
- Settings UI with enable toggle, port, token (with random Generate button), live status indicator
- API documentation in `docs/remote-api.md` for community integrators

---

## Milestone 22 — Multi-Host Support ✓ Complete (`0.6.0-alpha.1`)

Manage Hyper-V VMs on remote machines on the LAN.

- `HyperVHost` model and `HyperVHosts` SQLite table; DPAPI-encrypted credentials
- `IHyperVHostService` manages the local + remote host registry; `TestConnectionAsync` probes remote WMI namespace
- `BattlegroupProfile.HostId` links profiles to their target host (null = local)

---

## Milestone 23 — Player Management ✓ Complete (`0.6.0-alpha.1`)

Surface in-game admin commands in the UI.

- Log-based player detection from `OutputLineReceived`
- Kick command via stdin (`IServerProcessService.SendCommandAsync`)
- Ban records persisted to SQLite; timed or permanent bans
- Allowlist by Steam ID with optional display name
- Players view with Connected Players, Ban List, and Allowlist tabs

---

## Milestone 24 — Server Metrics Dashboard ✓ Complete (`0.6.0-alpha.1`)

Historical server health data surfaced as charts.

- 60-second periodic snapshot collection (CPU %, memory MB, player count, uptime)
- OxyPlot line charts for player count, CPU, and memory
- Downtime event tracking: opened on unexpected server exit, closed on restart
- Uptime availability percentage per time window
- 30-day snapshot retention with automatic pruning

---

## Milestone 25 — Mod Management ✗ Cancelled

Cancelled before implementation due to Funcom IP licensing restrictions. The four issues (#166–#169) were closed without code being written.

---

## Milestone 28 — UX Hardening and Friction Reduction ✓ Complete (`0.1.0-alpha.3` / `0.4.0-alpha.1`)

Addressed quality-of-life gaps and security improvements identified during early alpha testing.

- Toast / snackbar notification system for all async operation results
- Setup Wizard install path validation (write access, free disk space, drive root guard)
- Setup Wizard token validation (auto-trim, length hint)
- Networking view: adapter picker for multi-NIC hosts; UAC cancel feedback
- Log view: "Copy Logs" toolbar button
- Setup Wizard Progress step: cancel button and 30-minute timeout detection
- Settings view: raw INI editor sync warning when structured form also has unsaved changes
- Log file auto-refresh via `FileSystemWatcher`
- Dashboard quick-access shortcuts disabled when install path does not exist
- Backups view: restore progress message before final result
- `IActiveProfileService` singleton — active profile resolved once per session, eliminating redundant DB queries
- DPAPI encryption at rest for the Dune account token and admin password

---

## Milestone 26 — Discord Webhook Integration ✓ Complete (Unreleased)

Notify a Discord channel of server events.

- `IDiscordWebhookService` posts colour-coded embeds via `HttpClient`; fire-and-forget with one retry on HTTP 5xx
- Server lifecycle events: started (green), stopped normally (orange), crashed (red)
- Per-event toggle settings — each notification type can be enabled or disabled independently
- Manual announcement panel on the Dashboard — free-text message sent as a blue embed
- Webhook configuration in Settings: URL field, Test button, Save, enable toggle
- Settings persisted to `ApplicationSettings` with `ALTER TABLE` migration for existing databases

---

## Post-MVP / Future Considerations

These are planned milestones with open GitHub issues, not yet implemented.

- **Milestone 27 — Backup Cloud Sync** — optional sync of backups to OneDrive (Microsoft Graph), S3-compatible storage (AWS, Backblaze, MinIO), with a provider abstraction layer and restore flow

---

## Version Strategy

| Range | Phase |
|-------|-------|
| `0.1.x-alpha.N` | Internal alpha — UI foundation, stub integrations ✓ |
| `0.2.x-alpha.N` | Hyper-V + server process integration complete ✓ |
| `0.3.x-alpha.N` | Save data backups, polish, app log viewer ✓ |
| `0.4.x-alpha.N` | Auto-update, multiple profiles ✓ |
| `0.5.x-alpha.N` | Remote management web dashboard ✓ |
| `0.6.x-alpha.N` | Multi-host support, player management, server metrics ✓ |
| `1.0.0` | First stable release |

---

## Feedback

Feature requests are tracked as GitHub issues. Open one at [github.com/michaelstoffer/sietch-console/issues](https://github.com/michaelstoffer/sietch-console/issues) with the `enhancement` label. Include a description of the problem you are trying to solve, not just the feature you want.
