# Sietch Console

> The easiest way to self-host a Dune: Awakening battlegroup.

Sietch Console is a Windows-native desktop application that replaces the manual scripts, terminal windows, and raw INI editing required to host a Dune: Awakening dedicated server. It provides a single, modern UI for setup, configuration, monitoring, diagnostics, backups, and networking — designed for both non-technical players and advanced server hosts.

---

## Features

| Area | What it does |
|------|-------------|
| **Setup Wizard** | Guided multi-step onboarding — validates requirements, configures the Hyper-V VM, and saves your battlegroup profile |
| **Dashboard** | One-click Start / Stop / Restart with live status indicator |
| **Configuration** | Structured INI editor for server identity, gameplay settings, and network options; auto-backup before every save |
| **Logs** | Live log streaming with search, severity filtering, and detected-issue explanations |
| **Diagnostics** | On-demand system readiness checks with friendly pass/warning/failure results and remediation guidance |
| **Backups** | Create and restore configuration and save-data backups with a full history |
| **Networking** | Host and VM IP detection, Windows Firewall rule checker and auto-creator, port forwarding guide, local port test, copyable connection summary |
| **Settings** | Structured settings editor and raw INI editor with per-file selection |

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
3. Work through each step — the wizard checks your system, asks for your install path and Funcom token, and configures your battlegroup.
4. Once setup is complete, use the **Dashboard** to start your server.
5. Share your connection details from the **Networking** tab.

---

## Architecture

```
Sietch Console (WPF .NET 8)
├── Views/              XAML views — one per navigation section
├── ViewModels/         CommunityToolkit.Mvvm ObservableObject classes
├── Services/           Application-layer services (Backups, Networking, …)
├── Converters/         IValueConverter implementations
└── Themes/             SietchTheme.xaml — global brushes, styles, templates

SietchConsole.Core
├── Models/             Sealed record types (domain objects)
└── Interfaces/         Service contracts (IBackupService, INetworkingService, …)

SietchConsole.Data
├── Entities/           EF Core entity classes
├── Repositories/       IRepository<T> implementations over SQLite
└── Migrations/         EF Core migration history
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
