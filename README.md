<p align="center">
  <img src="Sietch Console/Resources/AppIcon-256.png" width="128" alt="Sietch Console" />
</p>

<h1 align="center">Sietch Console</h1>

<p align="center">
  <strong>The easiest way to self-host a Dune: Awakening battlegroup.</strong>
</p>

<p align="center">
  A Windows-native server manager for setup, operations, backups, logs,
  diagnostics, networking, remote control, and day-to-day battlegroup hosting.
</p>

<p align="center">
  <a href="https://github.com/Nerrowake/sietch-console/actions/workflows/build.yml"><img alt="CI" src="https://github.com/Nerrowake/sietch-console/actions/workflows/build.yml/badge.svg"></a>
  <a href="https://github.com/Nerrowake/sietch-console/actions/workflows/release.yml"><img alt="Release" src="https://github.com/Nerrowake/sietch-console/actions/workflows/release.yml/badge.svg"></a>
  <a href="https://github.com/Nerrowake/sietch-console/releases/latest"><img alt="GitHub Release" src="https://img.shields.io/github/v/release/Nerrowake/sietch-console?include_prereleases&label=version"></a>
  <a href="LICENSE.md"><img alt="License: MIT" src="https://img.shields.io/badge/license-MIT-green"></a>
  <a href="INSTALL.md"><img alt="Platform" src="https://img.shields.io/badge/platform-Windows%2010%2F11-0078D4?logo=windows"></a>
  <a href="https://dotnet.microsoft.com/download/dotnet/8.0"><img alt=".NET 8" src="https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet"></a>
</p>

---

## Overview

Sietch Console replaces the manual scripts, terminal windows, and raw INI
editing usually required to host a Dune: Awakening dedicated server.

It gives server hosts a single, modern desktop UI for automated server download
via SteamCMD, Hyper-V VM management, live process control, log streaming,
configuration, diagnostics, backups, networking, remote management, player tools,
metrics, and Discord announcements.

It is built for non-technical players who want to host with confidence, while
still exposing enough operational detail for advanced server hosts.

---

## Features

| Area | What it does |
| --- | --- |
| **Setup Wizard** | Guided onboarding that validates requirements, downloads server files via SteamCMD, configures the Hyper-V VM, and saves your battlegroup profile. |
| **Dashboard** | One-click Start, Stop, Restart, and Update with live uptime, CPU/RAM badges, contextual actions, shortcut tiles, and Discord announcements. |
| **Configuration** | Structured INI editor for server identity, gameplay settings, and network options, with auto-backup before every save. |
| **Logs** | Live stdout/stderr from the running server plus file-based tailing, search, severity filtering, and detected issue explanations. |
| **Diagnostics** | On-demand system readiness checks with clear pass, warning, and failure results plus remediation guidance. |
| **Backups** | Config, save-data, and full backups with history, restore, scheduled auto-backups, retention settings, and optional cloud sync. |
| **Networking** | Host and VM IP detection, firewall rule checks, auto-created Windows Firewall rules, port guidance, local port tests, and copyable connection summaries. |
| **Profiles** | Create, switch, and delete battlegroup profiles from the sidebar. All views update when the active profile changes. |
| **App Logs** | In-app log viewer for Sietch Console itself, with level and keyword filtering. |
| **Auto-Update** | Startup release checks, update banner, and one-click download/install flow. |
| **Remote Management** | Embedded LAN web dashboard with status, controls, live log tail, REST API, SSE updates, bearer token auth, and rate limiting. |
| **Players** | Live connected-player list, kick, timed and permanent bans, ban list, unban support, and Steam ID allowlist. |
| **Metrics** | CPU, memory, uptime snapshots, availability percentage, downtime events, and selectable time ranges. |
| **Multi-Host** | Remote Hyper-V host registration, DPAPI-encrypted WMI credentials, connection testing, and profile-to-host linking. |
| **Discord Webhooks** | Event-specific webhook notifications for server start, stop, crash, and manual community announcements. |

---

## Requirements

- Windows 10 20H1 build 19041 or Windows 11, x64
- Windows Pro, Enterprise, or Education edition with Hyper-V enabled
- 8 GB RAM minimum, 16 GB recommended when running the VM at the same time
- No separate .NET runtime required for normal use; the installer is
  self-contained

> Sietch Console manages a Dune: Awakening server running inside a Hyper-V
> virtual machine. Windows Home is not supported because Hyper-V is unavailable
> there.

---

## Installation

Download the latest release from the
[Releases page](https://github.com/Nerrowake/sietch-console/releases).

Release options:

- `SietchConsole-<version>-Setup.exe` - Windows installer with Start Menu and
  optional desktop shortcut
- `SietchConsole-<version>-win-x64-portable.zip` - portable build that can be
  extracted and run from any folder

Full installation and first-launch instructions are in [INSTALL.md](INSTALL.md).

---

## Quick Start

1. Install and launch Sietch Console.
2. The Setup Wizard opens automatically on first launch.
3. Work through each step. The wizard checks your system, downloads the server
   files, configures the Hyper-V VM, and saves your battlegroup profile.
4. Use the Dashboard to start your server.
5. Watch Logs for live output and status.
6. Share connection details from the Networking tab.

---

## Architecture

```text
Sietch Console (WPF .NET 8)
├── Views/              XAML views, one per navigation section
├── ViewModels/         CommunityToolkit.Mvvm ObservableObject classes
├── Services/           Application services for control, backups, players, metrics, and more
├── Converters/         IValueConverter implementations
└── Themes/             SietchTheme.xaml global brushes, styles, and templates

SietchConsole.Core
├── Models/             Domain records
└── Interfaces/         Service contracts

SietchConsole.Data
├── Repositories/       EF Core and SQLite repository implementations
└── Database/           DbContext and DatabaseInitializerService
```

The application uses `Microsoft.Extensions.Hosting` for dependency injection and
startup orchestration. ViewModels are singletons. Database repositories are
scoped and accessed through `IServiceScopeFactory`.

Full architecture details are in [docs/architecture.md](docs/architecture.md).

---

## Development Setup

Prerequisites:

- Windows 10/11 with Hyper-V enabled
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Visual Studio 2022, JetBrains Rider, or another .NET-capable IDE
- Git

Clone and build:

```bash
git clone https://github.com/Nerrowake/sietch-console.git
cd sietch-console
dotnet restore
dotnet build
```

Run:

```bash
dotnet run --project "Sietch Console/Sietch Console.csproj"
```

Versioning:

The current version is set in [Directory.Build.props](Directory.Build.props). To
release, update the version there, update [CHANGELOG.md](CHANGELOG.md), and push
a matching `v*.*.*` tag.

---

## Documentation

- [Installation](INSTALL.md)
- [User Setup Guide](docs/user-setup-guide.md)
- [Troubleshooting](docs/troubleshooting.md)
- [Architecture](docs/architecture.md)
- [Remote API](docs/remote-api.md)
- [Release Checklist](docs/release-checklist.md)
- [Known Limitations](docs/known-limitations.md)
- [Roadmap](docs/roadmap.md)

---

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for branch naming, pull request process,
coding standards, and the milestone workflow.

---

## Disclaimer

Sietch Console is an unofficial community project. It is not affiliated with,
endorsed by, or sponsored by Funcom.

Dune: Awakening and all related assets, trademarks, and intellectual property
belong to their respective owners.

---

## License

MIT. See [LICENSE.md](LICENSE.md) for details.
