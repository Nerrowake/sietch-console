# Sietch Console

> The easiest way to self-host a Dune: Awakening server.

Sietch Console is a Windows-native server manager for self-hosted Dune: Awakening battlegroups. It simplifies setup, configuration, updates, networking, diagnostics, backups, and server management through a modern desktop UI instead of raw scripts, terminals, and manual INI editing.

This project is designed for both non-technical players and advanced server hosts who want a cleaner, safer, and more approachable way to manage Dune: Awakening dedicated servers.

---

# Disclaimer

Sietch Console is an unofficial community project and is not affiliated with, endorsed by, or sponsored by Funcom.

Dune: Awakening and all related assets, trademarks, and intellectual property belong to their respective owners.

---

# Product Goals

The primary goal of Sietch Console is to dramatically reduce the complexity of hosting a Dune: Awakening battlegroup.

The application should:

- Simplify the official self-hosted server workflow
- Reduce reliance on command-line interfaces
- Make diagnostics understandable for non-technical users
- Provide a safe configuration management experience
- Reduce setup and networking frustration
- Make battlegroup hosting approachable for small communities and friend groups
- Preserve advanced workflows for power users without overwhelming beginners

---

# MVP Scope

Version 1 focuses on replacing the most painful parts of the current setup experience.

## Included Features

- System readiness checker
- Hyper-V validation
- Steam and server package detection
- Guided setup wizard
- Battlegroup start/stop/restart controls
- Configuration management UI
- INI parsing and editing
- Live log viewing
- Friendly diagnostics and troubleshooting
- Backup and restore workflows
- Networking and firewall guidance
- Windows-native desktop experience
- Local SQLite persistence
- Automatic configuration backups

---

# Non-Goals for Version 1

The following features are intentionally excluded from the initial release:

- Remote cloud orchestration
- Linux-native hosting
- Multi-machine cluster management
- Mobile applications
- Plugin systems
- Dedicated web dashboard
- Kubernetes-native administration
- Advanced VM orchestration
- Discord bot integration
- Multi-user authentication systems
- Cross-platform desktop support

These may be explored in future versions after the core hosting experience is stable.

---

# Official Dune Server Architecture

The current Dune: Awakening self-hosted server environment operates using the following structure:

```text
Windows Host
└── Hyper-V
    └── Alpine Linux VM
        └── Kubernetes Cluster
            └── Dune Server Pods
```

Sietch Console does not replace this architecture.

Instead, it acts as a management and orchestration layer that simplifies setup, monitoring, configuration, and troubleshooting.

---

# Planned Application Architecture

```text
Sietch Console
├── WPF UI Layer
├── MVVM ViewModels
├── Application Services
├── Infrastructure Services
│   ├── HyperVService
│   ├── SteamService
│   ├── ProcessService
│   ├── ConfigService
│   ├── DiagnosticsService
│   ├── BackupService
│   ├── NetworkService
│   └── LogService
├── SQLite Local Storage
└── Battlegroup Integration Layer
```

---

# Technology Stack

## Frontend

- WPF
- XAML
- MVVM Architecture
- CommunityToolkit.Mvvm

## Backend / Services

- .NET 8
- C#
- Windows APIs
- PowerShell Integration
- Process Management APIs

## Storage

- SQLite

## Build & Tooling

- Visual Studio 2022+
- GitHub Actions
- Git
- NuGet

---

# Repository Structure

```text
/src
    /SietchConsole.UI
    /SietchConsole.Core
    /SietchConsole.Infrastructure
    /SietchConsole.Services
    /SietchConsole.Data
    /SietchConsole.Models

/docs
    architecture
    roadmap
    setup
    troubleshooting

/assets
    branding
    icons
    screenshots

/scripts
/tests
```

---

# Development Setup

## Requirements

Before developing locally, ensure the following are installed:

- Windows 10/11 Pro
- Hyper-V enabled
- Visual Studio 2022
- .NET 8 SDK
- Git

---

## Clone Repository

```bash
git clone https://github.com/YOUR_USERNAME/sietch-console.git
cd sietch-console
```

---

## Open Solution

Open the solution in Visual Studio:

```text
SietchConsole.sln
```

---

## Restore Dependencies

```bash
dotnet restore
```

---

## Build Project

```bash
dotnet build
```

---

## Run Application

```bash
dotnet run
```

---

# Coding Standards

## General Principles

- Prefer readability over cleverness
- Keep services focused and small
- Avoid unnecessary abstraction
- Use explicit naming
- Maintain clear separation of concerns
- Prioritize maintainability over premature optimization

---

## Architecture Rules

- UI logic belongs in ViewModels
- Business logic belongs in Services
- Infrastructure logic belongs in Infrastructure layer
- Models should remain simple and predictable
- Avoid tightly coupling UI to infrastructure services
- Prefer dependency injection for service access

---

# AI-Assisted Development Guidelines

This repository is intentionally structured for AI-assisted development workflows.

When generating code with AI tools:

- Keep changes scoped to a single issue whenever possible
- Avoid large-scale architectural rewrites
- Preserve project structure and naming conventions
- Follow the milestone and issue system defined in GitHub
- Do not introduce new frameworks without clear justification
- Prefer maintainable implementations over experimental patterns
- Keep generated code readable and conventional

AI-generated code should always be reviewed before merging.

---

# Roadmap Overview

## Milestone 1: Product Foundation

Establish the overall product vision, architecture, branding, documentation, repository structure, and development standards for the project.

Core focus areas:

- Product planning
- Technical architecture
- Branding and identity
- Repository organization
- AI-assisted workflow standards

---

## Milestone 2: Windows Application Shell

Build the foundational WPF desktop application structure using .NET 8 and MVVM architecture.

Core focus areas:

- Main application shell
- Navigation system
- Base UI layout
- Global theming
- Placeholder screens
- Dependency injection setup

---

## Milestone 3: Local Data Layer

Implement persistent local storage and state management using SQLite.

Core focus areas:

- SQLite integration
- Application settings
- Battlegroup profiles
- Backup metadata
- Diagnostics history
- Repository pattern

---

## Milestone 4: System Readiness Checker

Create diagnostics systems to verify whether the host machine is capable of running a Dune: Awakening battlegroup.

Core focus areas:

- Windows validation
- Hyper-V checks
- Virtualization detection
- Memory and storage checks
- Firewall diagnostics
- Networking diagnostics
- Friendly remediation guidance

---

## Milestone 5: Setup Wizard

Build the guided onboarding and installation workflow for new battlegroup hosts.

Core focus areas:

- Setup wizard navigation
- VM configuration
- Token management
- Battlegroup setup
- Installation progress tracking
- Recovery and resume support

---

## Milestone 6: Steam and Server Installation

Integrate directly with the official Dune server package and setup scripts.

Core focus areas:

- Steam detection
- Server package detection
- Setup script execution
- Console output streaming
- Installation state management
- Setup error handling

---

## Milestone 7: Battlegroup Control Center

Create the primary battlegroup management interface for server control and monitoring.

Core focus areas:

- Start and stop controls
- Restart workflows
- Runtime monitoring
- VM interaction
- Status refresh systems
- Battlegroup management actions

---

## Milestone 8: Configuration Management

Replace manual INI editing with a structured and validated configuration management system.

Core focus areas:

- INI parsing
- Configuration editing
- Validation systems
- Settings UI
- Advanced editing support
- Automatic configuration backups

---

## Milestone 9: Logs and Diagnostics

Transform technical logs and failures into understandable diagnostics and troubleshooting workflows.

Core focus areas:

- Live log streaming
- Error detection
- Diagnostics summaries
- Friendly error explanations
- Troubleshooting recommendations
- Exportable diagnostics reports

---

## Milestone 10: Backup and Restore

Protect battlegroup configurations and server data through automated backup and recovery workflows.

Core focus areas:

- Backup creation
- Restore workflows
- Backup metadata
- Backup management UI
- Restore validation
- Automatic restore points

---

## Milestone 11: Networking Assistant

Simplify networking and external player connectivity for non-technical users.

Core focus areas:

- Port visibility
- Firewall detection
- Firewall repair helpers
- VM networking diagnostics
- Port forwarding guidance
- External connectivity validation

---

## Milestone 12: User Interface Polish and Experience

Refine the visual design and overall user experience to reach a polished commercial-quality standard.

Core focus areas:

- Final branding
- Dashboard improvements
- Loading states
- Error states
- Accessibility improvements
- Responsive layouts
- Onboarding copy
- Visual consistency

---

## Milestone 13: Installer and Release Pipeline

Prepare the application for public distribution and release management.

Core focus areas:

- Windows installer
- Versioning system
- GitHub Actions automation
- Packaging workflows
- Alpha release preparation
- Release documentation

---

## Milestone 14: Documentation and Long-Term Maintainability

Create comprehensive project documentation and standards for future development and support.

Core focus areas:

- Technical architecture documentation
- Setup guides
- Troubleshooting documentation
- Contribution guidelines
- Known limitations tracking
- Product roadmap maintenance
- AI-assisted development standards

---

# Current Status

Project status: Planning / Pre-Alpha

The repository is currently focused on architecture, milestone planning, and foundational application setup.

---

# Contributing

Contribution guidelines will be added in a future milestone.

---

# License

License to be determined.

---

# Acknowledgements

Special thanks to the Dune: Awakening community and the official self-hosted server documentation for making community-hosted battlegroups possible.
