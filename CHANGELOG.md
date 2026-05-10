# Changelog

All notable changes to Sietch Console are documented in this file.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
Sietch Console uses [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

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

[Unreleased]: https://github.com/michaelstoffer/sietch-console/compare/v0.1.0-alpha.2...HEAD
[0.1.0-alpha.2]: https://github.com/michaelstoffer/sietch-console/releases/tag/v0.1.0-alpha.2
[0.1.0-alpha.1]: https://github.com/michaelstoffer/sietch-console/releases/tag/v0.1.0-alpha.1
