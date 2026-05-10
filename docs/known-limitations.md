# Known Limitations

This document lists known bugs, unimplemented features, and unsupported configurations in the current alpha release. Before filing a bug report, check whether the issue is already listed here.

---

## Alpha Scope

Sietch Console `0.4.0-alpha.1` is an **internal alpha**. The application shell, UI, and all views are implemented. Hyper-V VM control (start, stop, restart, provisioning, live resource display), SteamCMD server file download and update, live server process management (stdout/stderr streaming, server-ready detection, crash surfacing, graceful shutdown), full + scheduled backups with retention, multiple battleground profiles, in-app application log viewer, VM IP auto-detection, and GitHub Releases auto-update are all fully implemented.

---

## Known Bugs

### Setup Wizard re-opens if database was partially written

If the Setup Wizard is interrupted mid-way and you relaunch, it may either skip the wizard (if a partial profile record was written) or show the wizard from the beginning (if nothing was committed). The "Resume Previous Setup?" overlay offers to pick up where you left off, but intermediate step state may be incomplete if the write was interrupted before the last step was committed.

**Workaround:** Use "Start Fresh" in the resume overlay, or delete `%LOCALAPPDATA%\SietchConsole\sietch.db` and relaunch to start completely clean.

### Settings view shows "no config" if the INI path doesn't exist

If the server files haven't been installed, the Settings view shows an empty state because no INI file exists at the expected path. This is technically correct behavior, but the message could be clearer about the root cause.

### Networking tab may show the wrong local IP

The host IP detection enumerates Windows network interfaces and picks the first non-loopback IPv4 address. On machines with multiple adapters (VPN clients, Hyper-V virtual switches, Docker), this may select the wrong interface.

**Workaround:** Note the "Host IP" shown and verify it against your actual LAN IP (`ipconfig` in a terminal).

### Server-ready detection depends on log output patterns

The Dashboard transitions to "Running" once `ServerProcessService` detects a server-ready phrase in stdout (e.g. "listening on port"). If the Dune: Awakening server uses different log output, the Dashboard may remain in "Starting" even after the server is accepting connections.

**Workaround:** If the server is accepting connections but the status shows Starting, it is functional -- the pattern match simply hasn't fired. This will be tuned as the server's log output becomes known.

### Dune: Awakening dedicated server App ID is unverified

`ServerPackageInstaller` uses App ID `2369390` for the Dune: Awakening dedicated server. This ID has not been officially confirmed by Funcom. If SteamCMD downloads the wrong app or reports an error, the App ID may need to be updated.

### Remote dashboard player count is always 0 and uptime is always —

The remote dashboard's player count and uptime fields are not yet populated. Player count requires a server query API that Funcom has not made available. Uptime would require persisting the server start time — this is planned but not yet implemented. Both fields display placeholder values in the current release.

---

## Unsupported Configurations

### Windows Home

Hyper-V is not available on Windows Home. Sietch Console cannot run on Windows Home. There is no planned workaround -- the application is architecturally dependent on Hyper-V.

### Non-x64 hardware

The application is published as a single-file self-contained win-x64 binary. It will not run on 32-bit Windows or ARM-based Windows devices (e.g., Snapdragon X).

### Running the VM on a separate machine

Sietch Console assumes the Hyper-V host and the user's desktop are the same machine. Running the VM on a remote Hyper-V host is not supported and is not planned for the current phase.

### Server process inside the VM (PowerShell Direct)

The server process is currently spawned directly on the host machine from `InstallPath`. When a Hyper-V VM is configured, the VM is started first but the server executable still runs on the host. Running the server inside the VM guest via PowerShell Direct is not yet implemented.

### Linux or macOS

This is a WPF application targeting `net8.0-windows`. It does not run on Linux or macOS.

---

## Missing Features (Planned)

These features are not present in the alpha and are planned for future milestones:

- Code-signed installer (removes the SmartScreen warning -- deferred, requires a real certificate)
- Application icon (`.ico`) -- the installer and taskbar currently use a placeholder
- Splash screen on startup
- Server process inside the Hyper-V guest via PowerShell Direct
- Player management (kick, ban, allowlist)
- Mod management (listing, enable/disable, updates)
- Server metrics history charts (CPU/memory over time, player count history)
- Webhook / notification integrations (server lifecycle events)
- Remote management player count and uptime (requires server-side API from Funcom)
- Cloud backup destinations (S3, OneDrive, MinIO)

---

## Reporting New Issues

If you encounter a problem not listed here, open an issue at [github.com/michaelstoffer/sietch-console/issues](https://github.com/michaelstoffer/sietch-console/issues).

Please include:

- Sietch Console version
- Windows version (`winver`)
- Steps to reproduce
- Expected vs. actual behavior
- Diagnostics output (from the Diagnostics tab)
- App Logs output (from the App Logs tab -- helps diagnose service-layer failures)
