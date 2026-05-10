# Known Limitations

This document lists known bugs, unimplemented features, and unsupported configurations in the current alpha release. Before filing a bug report, check whether the issue is already listed here.

---

## Alpha Scope

Sietch Console `0.1.0-alpha.4` is an **internal alpha**. The application shell, UI, and all views are implemented. Hyper-V VM control (start, stop, restart, provisioning, and live resource display) is fully implemented. Server file download and server process management remain stubs — the application cannot yet automatically install server files or spawn the game server process inside the VM.

---

## Stub / Not-Yet-Implemented

### Server Process Management

**Status:** Stub  
The application does not spawn, monitor, or communicate with a Dune: Awakening server process. Log streaming reads from a file path if one exists, but no process output is captured.

**Impact:** Logs will be empty unless a log file was written by an external process.

### SteamCMD / Server Package Download

**Status:** Stub  
`ServerPackageService` and `SteamDetectionService` do not perform real Steam detection or file downloads. The Setup Wizard's Progress step simulates completion without downloading or installing server files.

**Impact:** After completing the Setup Wizard, no server files exist on disk. Manual installation is required.

### Setup Script Execution

**Status:** Stub  
`SetupScriptService` does not execute any scripts or create Hyper-V VM configurations. The wizard completes but does not configure a VM.

---

## Known Bugs

### Setup Wizard re-opens if database was partially written

If the Setup Wizard is interrupted mid-way and you relaunch, it may either skip the wizard (if a partial profile record was written) or show the wizard from the beginning (if nothing was committed). There is no resume-from-step capability.

**Workaround:** Delete `%LOCALAPPDATA%\SietchConsole\sietch.db` and relaunch to restart cleanly.

### Settings view shows "no config" if the INI path doesn't exist

If the server files haven't been installed (see stub above), the Settings view shows an empty state because no INI file exists at the expected path. This is technically correct behavior, but the message could be clearer about the root cause.

### Networking tab may show the wrong local IP

The host IP detection enumerates Windows network interfaces and picks the first non-loopback IPv4 address. On machines with multiple adapters (VPN clients, Hyper-V virtual switches, Docker), this may select the wrong interface.

**Workaround:** Note the "Host IP" shown and verify it against your actual LAN IP (`ipconfig` in a terminal).

### Log file selector does not auto-refresh

If a log file is created after the Logs view is opened, it does not appear in the file selector until the view is navigated away from and back.

---

## Unsupported Configurations

### Windows Home

Hyper-V is not available on Windows Home. Sietch Console cannot run on Windows Home. There is no planned workaround — the application is architecturally dependent on Hyper-V.

### Non-x64 hardware

The application is published as a single-file self-contained win-x64 binary. It will not run on 32-bit Windows or ARM-based Windows devices (e.g., Snapdragon X).

### Running the VM on a separate machine

Sietch Console assumes the Hyper-V host and the user's desktop are the same machine. Running the VM on a remote Hyper-V host is not supported and is not planned.

### Multiple battlegroup profiles

Only one battlegroup profile is supported at a time in the current alpha. The database schema is keyed to a single active profile. Multi-profile support may be added in a future release.

### Linux or macOS

This is a WPF application targeting `net8.0-windows`. It does not run on Linux or macOS.

---

## Missing Features (Planned)

These features are not present in the alpha and are planned for future milestones:

- SteamCMD integration for automated server file download and updates
- Auto-update mechanism for Sietch Console itself
- Code-signed installer (removes the SmartScreen warning)
- Application icon (`.ico`) — the installer and taskbar currently use a placeholder
- Splash screen on startup
- Server save-data backup (Config backup works; Save Data backup is scaffolded but not wired to a real path)
- Scheduled automatic backups
- Multiple battlegroup profiles
- Dark/light theme toggle
- In-app log viewing for the Sietch Console application log (not the game server log)

---

## Reporting New Issues

If you encounter a problem not listed here, open an issue at [github.com/michaelstoffer/sietch-console/issues](https://github.com/michaelstoffer/sietch-console/issues).

Please include:

- Sietch Console version
- Windows version (`winver`)
- Steps to reproduce
- Expected vs. actual behavior
- Diagnostics output (from the Diagnostics tab)
