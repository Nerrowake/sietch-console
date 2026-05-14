<p align="center">
  <img src="Sietch Console/Resources/AppIcon-256.png" width="128" alt="Sietch Console" />
</p>

# Sietch Console

> The easiest way to self-host a Dune: Awakening battlegroup.

[![CI](https://github.com/michaelstoffer/sietch-console/actions/workflows/build.yml/badge.svg)](https://github.com/michaelstoffer/sietch-console/actions/workflows/build.yml)
[![Release](https://github.com/michaelstoffer/sietch-console/actions/workflows/release.yml/badge.svg)](https://github.com/michaelstoffer/sietch-console/actions/workflows/release.yml)
[![GitHub Release](https://img.shields.io/github/v/release/michaelstoffer/sietch-console?include_prereleases&label=version)](https://github.com/michaelstoffer/sietch-console/releases/latest)
[![License: MIT](https://img.shields.io/badge/license-MIT-green)](LICENSE)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-0078D4?logo=windows)](https://github.com/michaelstoffer/sietch-console/blob/main/INSTALL.md)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![GitHub Sponsors](https://img.shields.io/badge/sponsor-%E2%9D%A4-ea4aaa?logo=github-sponsors)](https://github.com/sponsors/michaelstoffer)

Sietch Console is a Windows-native desktop application that replaces the manual scripts, terminal windows, and raw INI editing required to host a Dune: Awakening dedicated server. It provides a single, modern UI for automated server download via SteamCMD, Hyper-V VM management, live server process control, log streaming, configuration, diagnostics, backups, and networking — designed for both non-technical players and advanced server hosts.

---

## Features

| Area | What it does |
|------|-------------|
| **Setup Wizard** | Guided multi-step onboarding — validates requirements, downloads server files via SteamCMD, configures the Hyper-V VM, and saves your battlegroup profile |
| **Dashboard** | One-click Start / Stop / Restart with real Hyper-V VM control and server process management; live status, CPU/RAM display, update-available indicator, and one-click server update |
| **Configuration** | Structured INI editor for server identity, gameplay settings, and network options; auto-backup before every save |
| **Logs** | Live stdout/stderr stream from the running server process (with a LIVE badge) plus file-based tail; search, severity filtering, and detected-issue explanations |
| **Diagnostics** | On-demand system readiness checks with friendly pass/warning/failure results and remediation guidance |
| **Backups** | Config, save-data, and full (config + save-data) backups with full history, one-click restore, scheduled auto-backups, and configurable retention; optional cloud sync to OneDrive or any S3-compatible provider (AWS, Backblaze B2, MinIO, Cloudflare R2) with upload, restore, and delete |
| **Networking** | Host and VM IP detection (auto-persisted to profile), Windows Firewall rule checker and auto-creator, port forwarding guide, local port test, copyable connection summary |
| **Settings** | Structured settings editor and raw INI editor with per-file selection |
| **Multiple profiles** | Create, switch, and delete battleground profiles from the sidebar; all views update instantly when you switch |
| **App Logs** | In-app viewer for Sietch Console's own log stream — filter by level or keyword to diagnose service issues without opening external files |
| **Auto-Update** | On-startup check against GitHub Releases; banner notification when a new version is available; one-click download and install |
| **Remote Management** | Embedded web dashboard accessible from any browser on the LAN — status, controls, and live log tail; REST + SSE API for custom integrations; Bearer token auth with rate limiting |
| **Players** | Live connected-player list (log-based detection), kick and timed/permanent ban commands, ban list with unban support, and Steam ID allowlist — all stored in SQLite |
| **Metrics** | 60-second CPU, memory, and uptime snapshots charted over time with OxyPlot; availability percentage; downtime event log; configurable time-range selector (1 h – 30 d) |
| **Multi-Host** | Register remote Hyper-V hosts by hostname with DPAPI-encrypted WMI credentials; WMI connection test; link battleground profiles to a specific host |
| **Discord Webhooks** | Configurable webhook URL with per-event toggles (server start, stop, crash); colour-coded embed notifications sent automatically on lifecycle events; manual announcement panel on the Dashboard for broadcasting messages to your community |

---

## Requirements

- **Windows 10 20H1 (build 19041) or Windows 11** — x64
- **Hyper-V enabled** — requires Windows Pro, Enterprise, or Education edition
- **8 GB RAM** minimum (16 GB recommended when running the VM simultaneously)
- **.NET runtime** — not required; the installer includes a self-contained runtime

> Sietch Console manages a Dune: Awakening server running inside a Hyper-V virtual machine. The application does not run on Windows Home because Hyper-V is unavailable there.

---

## Installation

Download the latest release from the [Releases page](https://github.com/michaelstoffer/sietch-console/releases).

Two options are available:

- **`SietchConsole-<version>-Setup.exe`** — Windows installer with Start Menu and optional desktop shortcut
- **`SietchConsole-<version>-win-x64-portable.zip`** — extract and run `Sietch Console.exe` from any folder

Full installation and first-launch instructions: [INSTALL.md](INSTALL.md)

---

## Quick Start

1. Install and launch Sietch Console.
2. The **Setup Wizard** opens automatically on first launch.
3. Work through each step — the wizard checks your system, downloads the Dune: Awakening server files via SteamCMD, configures your Hyper-V VM, and saves your battlegroup profile.
4. Once setup is complete, use the **Dashboard** to start your server. The Logs tab shows live output; the Dashboard transitions to Running once the server is ready.
5. Share your connection details from the **Networking** tab.

---

## Architecture

```
Sietch Console (WPF .NET 8)
├── Views/              XAML views — one per navigation section
├── ViewModels/         CommunityToolkit.Mvvm ObservableObject classes
├── Services/           Application-layer services (Control, Backups, Players, Metrics, …)
├── Converters/         IValueConverter implementations
└── Themes/             SietchTheme.xaml — global brushes, styles, templates

SietchConsole.Core
├── Models/             Domain records (BattlegroupProfile, BanRecord, ServerMetricSnapshot, …)
└── Interfaces/         Service contracts (IPlayerManagementService, IMetricsCollectorService, …)

SietchConsole.Data
├── Repositories/       EF Core + SQLite repository implementations
└── Database/           DbContext, DatabaseInitializerService (schema migration)
```

The application uses **Microsoft.Extensions.Hosting** for dependency injection and startup orchestration. All ViewModels are singletons; database repositories are scoped and accessed via `IServiceScopeFactory`.

Full architecture details: [docs/architecture.md](docs/architecture.md)

---

## Development Setup

**Prerequisites**
- Windows 10/11 with Hyper-V enabled
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Visual Studio 2022+ or JetBrains Rider
- Git

**Clone and build**
```bash
git clone https://github.com/michaelstoffer/sietch-console.git
cd sietch-console
dotnet restore
dotnet build
```

**Run**
```bash
dotnet run --project "Sietch Console/Sietch Console.csproj"
```

**Versioning**
The current version is set in [`Directory.Build.props`](Directory.Build.props). To release, update the version there, commit, and push a matching `v*.*.*` tag — GitHub Actions handles the rest.

---

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for branch naming, PR process, coding standards, and the milestone/issue workflow.

---

## Disclaimer

Sietch Console is an unofficial community project. It is not affiliated with, endorsed by, or sponsored by Funcom.

Dune: Awakening and all related assets, trademarks, and intellectual property belong to their respective owners.

---

## License

MIT — see [LICENSE](LICENSE) for details.
