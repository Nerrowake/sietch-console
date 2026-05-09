# Changelog

All notable changes to Sietch Console are documented in this file.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
Sietch Console uses [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

---

## [0.1.0-alpha.1] — 2025-05-09

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

[Unreleased]: https://github.com/michaelstoffer/sietch-console/compare/v0.1.0-alpha.1...HEAD
[0.1.0-alpha.1]: https://github.com/michaelstouffer/sietch-console/releases/tag/v0.1.0-alpha.1
