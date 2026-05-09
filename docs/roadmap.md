# Roadmap

This document describes the planned feature trajectory for Sietch Console. Items are grouped by milestone. Ordering and scope are subject to change as the project evolves.

The current release is `0.1.0-alpha.1`. All items listed below are unimplemented as of this writing.

---

## Milestone 15 — Hyper-V Integration

Replace the stub server control layer with real Hyper-V operations.

- Provision a new Hyper-V VM (generation, memory, CPU, virtual switch) from within the app
- Start, stop, and restart the VM using PowerShell cmdlets via `System.Management.Automation`
- Poll VM state and surface live status on the Dashboard
- Display VM memory and CPU utilization in the Dashboard access shortcuts area
- Handle common Hyper-V errors with friendly messages (VM not found, insufficient resources, access denied)

---

## Milestone 16 — Server Package Management

Automate server file installation and updates.

- Detect SteamCMD on the host machine or download it automatically
- Download the Dune: Awakening dedicated server files via SteamCMD inside or outside the VM
- Verify installation integrity after download
- Check for server file updates and surface an "Update Available" indicator on the Dashboard
- One-click server update from the Dashboard (stop → update → restart)

---

## Milestone 17 — Live Server Integration

Connect the app to the running server process.

- Spawn and monitor the Dune: Awakening server process inside the VM
- Stream live stdout/stderr output to the Logs view
- Detect server-ready state from log output and transition Dashboard status to Running
- Capture the server process exit code and surface error states with explanations
- Support graceful shutdown (SIGTERM equivalent) vs. force stop

---

## Milestone 18 — Save Data Backups

Complete the backup system with save data support.

- Detect the Dune: Awakening save data directory path from the server configuration
- Implement save data backup: compress and archive to `%LOCALAPPDATA%\SietchConsole\Backups\`
- Implement save data restore: stop server, swap files, restart
- Full backup type: config + save data in one operation
- Scheduled automatic backups: configurable interval (e.g., every 6 hours) stored as a Windows Task Scheduler entry
- Backup pruning: keep last N backups, auto-delete older records

---

## Milestone 19 — Polish and Hardening

Address quality-of-life gaps and reliability issues from alpha feedback.

- Application icon (`.ico`) and taskbar icon — proper Windows app identity
- Splash screen on startup while the host initializes
- Code-signed installer — removes SmartScreen warning
- Setup Wizard resume support — if interrupted mid-setup, resume from the last completed step
- Multiple battlegroup profiles — create, switch between, and delete named profiles
- Hyper-V network adapter auto-detection for the VM IP on the Networking tab
- Log file selector auto-refresh when new files appear
- In-app application log viewer (Sietch Console's own logs, not the game server's)

---

## Milestone 20 — Auto-Update

Keep Sietch Console itself up to date.

- Check GitHub Releases API for a newer version on startup
- Display an "Update Available" banner with release notes summary
- One-click download and install of the new version (installer variant) or portable ZIP extraction
- Silent update option: download in the background, prompt to restart when ready

---

## Post-MVP / Future Considerations

These are ideas that may or may not be implemented, depending on community feedback and project direction.

- **Remote management** — a lightweight web UI for monitoring and control from a phone or secondary device
- **Multi-host support** — manage a VM running on a separate Hyper-V host on the local network
- **Player management** — in-game admin commands (kick, ban, allowlist) surfaced in the UI
- **Server metrics dashboard** — player count history, uptime graph, memory and CPU over time
- **Mod management** — list, enable, and update server-side mods if Funcom exposes a mod API
- **Discord webhook integration** — notify a Discord channel when the server starts, stops, or detects errors
- **Backup cloud sync** — optional sync of backups to OneDrive, Google Drive, or S3-compatible storage

---

## Version Strategy

| Range | Phase |
|-------|-------|
| `0.1.x-alpha.N` | Internal alpha — UI foundation, stub integrations |
| `0.2.x-alpha.N` | Hyper-V + server process integration complete |
| `0.3.x-beta.N` | Save data backups, polish, broader testing |
| `0.4.x-beta.N` | Auto-update, code signing, public beta |
| `1.0.0` | First stable release |

---

## Feedback

Feature requests are tracked as GitHub issues. Open one at [github.com/michaelstoffer/sietch-console/issues](https://github.com/michaelstoffer/sietch-console/issues) with the `enhancement` label. Include a description of the problem you are trying to solve, not just the feature you want.
