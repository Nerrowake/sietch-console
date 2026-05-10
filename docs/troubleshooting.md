# Troubleshooting

Common problems, diagnostic interpretation, and recovery steps for Sietch Console.

---

## Quick Diagnostic

Before anything else, go to the **Diagnostics** tab and click **Run Diagnostics**. The results tell you whether the most common sources of problems are present on your system. Each result has a severity badge:

| Badge | Meaning |
|-------|---------|
| **Pass** | This check is fine — not the source of your problem |
| **Warning** | Works, but something may cause issues later |
| **Failure** | This must be fixed before the server will work |

Click any result to expand its technical detail and remediation guidance.

---

## Installation and Launch Problems

### The application won't open after installation

1. Confirm you are running Windows 10 build 19041 or later: press `Win + R`, type `winver`, press Enter.
2. Confirm your OS is x64 (64-bit). The application does not run on 32-bit Windows.
3. Check **Event Viewer** → **Windows Logs** → **Application** for a crash event from `Sietch Console.exe`.

### SmartScreen blocks the installer

Click **More info** → **Run anyway**. The installer is not code-signed in early alpha builds. This is expected and will be resolved when code signing is added.

### Setup Wizard doesn't open on first launch

Delete `%LOCALAPPDATA%\SietchConsole\sietch.db` and relaunch. The wizard triggers when no profile is found in the database. If the database exists from a previous failed setup, the wizard is skipped incorrectly.

---

## Setup Wizard Problems

### Requirements check fails — Hyper-V not detected

1. Open `optionalfeatures.exe` (Windows Features).
2. Check **Hyper-V** and click **OK**.
3. Restart your computer.
4. Relaunch Sietch Console and run the Setup Wizard again.

> If you cannot find the Hyper-V option in Windows Features, your edition of Windows does not support it (Windows Home). Sietch Console cannot run on Windows Home.

### Requirements check fails — virtualization not enabled

Hyper-V requires hardware virtualization (Intel VT-x / AMD-V) to be enabled in your BIOS/UEFI.

1. Restart your computer.
2. Enter BIOS/UEFI setup (usually F2, F10, Del, or Esc during startup — varies by manufacturer).
3. Find the virtualization setting (often labeled **Intel Virtualization Technology**, **VT-x**, or **SVM Mode**).
4. Enable it and save.
5. Boot back into Windows and rerun the Setup Wizard.

### Requirements check fails — not enough RAM

The server VM requires at least 4 GB of RAM, plus RAM for Windows itself. If your system has less than 8 GB total, you will see this warning. The only fix is to install more RAM or reduce the VM memory allocation below the recommended minimum.

### Setup Wizard stalls on the Progress step

The VM configuration portion of the progress step is real — Sietch Console will attempt to provision the Hyper-V VM if it does not exist. The server file download step is still a stub and completes immediately. If the wizard appears to freeze during VM provisioning, check Hyper-V Manager for error events. If the application becomes unresponsive, close and relaunch — no data is lost and the wizard can be re-run.

---

## Dashboard and Server Control Problems

### The server shows "Offline" immediately after clicking Start

Check the **error banner** at the top of the Dashboard for a specific message. Common causes:

- **Access denied** — Sietch Console must run as Administrator to manage Hyper-V. Right-click the shortcut → **Run as administrator**.
- **VM not found** — the VM Name in your battlegroup profile must exactly match the name in Hyper-V Manager. Open Hyper-V Manager to confirm.
- **Hyper-V not enabled** — run the Diagnostics tab and check the Hyper-V readiness result.
- **VM starts but server shows Offline** — the VM may have powered on, but the game server process inside it is not yet running. Server process management is not automated in this alpha build; you must start the server process manually inside the VM for now.

### The status indicator never leaves "Starting"

Sietch Console polls VM state every 2 seconds for up to 90 seconds. If it does not reach Running within that window, it will time out and show an error. This usually means the VM is stuck booting (check Hyper-V Manager for the VM's state) or the VM was started from outside the app and is in an unexpected state.

---

## Configuration and Settings Problems

### Changes in Settings don't take effect

After saving, you must restart the server for any changes to take effect. Stop the server from the Dashboard, then start it again.

### "Settings file not found" empty state in the Settings view

The Settings view reads the battlegroup's INI configuration from the path recorded in the profile. If the path is wrong or the files haven't been created yet, the view shows an empty state. Complete the Setup Wizard to create the profile and install the server files.

### The INI editor shows garbled or unexpected content

The raw INI editor shows the file exactly as it exists on disk. If the server or another tool has written unexpected content, it will appear as-is. Use the **Revert** button to discard any unsaved changes and reload from disk.

---

## Backup and Restore Problems

### A backup restore fails silently

Restore overwrites the current server configuration files. If the files are locked (e.g., the server is running), the restore will fail. Stop the server first, then restore.

### Old backups are taking up a lot of disk space

Backups are stored in `%LOCALAPPDATA%\SietchConsole\Backups\`. Select old records in the Backups view and delete them using the **Delete** button. There is no automatic pruning in the current alpha.

---

## Networking Problems

### Firewall check fails — rule missing

Click **Create Firewall Rule** in the Networking tab. This will prompt for UAC elevation (administrator approval) and create the required inbound Windows Firewall rules automatically.

If UAC approval is denied, you can create the rules manually:

1. Open Windows Defender Firewall with Advanced Security.
2. Click **Inbound Rules** → **New Rule**.
3. Select **Port** → **UDP**.
4. Enter the required port numbers (visible in the Networking tab's port table).
5. Select **Allow the connection** and apply to all profiles.

### Players outside my local network can't connect

Your router must forward the server ports to your PC's local IP address. This is called port forwarding. The exact steps depend on your router model.

The Networking tab has a step-by-step port forwarding guide. The required ports are listed in the port table on the same page.

After setting up port forwarding:
1. Confirm your external IP hasn't changed (ISPs sometimes assign dynamic IPs).
2. Run the port test in the Networking tab.
3. Share your external IP, not your local IP, with players.

### Host IP shows a placeholder or wrong IP

The Networking tab detects your IP by inspecting Windows network interfaces. If it picks the wrong one (e.g., a VPN adapter or the Hyper-V virtual adapter), your real external IP may differ. Check [whatismyip.com](https://www.whatismyip.com) for your actual external address.

---

## Logs Problems

### Log view shows no entries

The Logs view reads from the log file path configured in your battlegroup profile. If the Dune: Awakening server process has not been started inside the VM, no log file is produced yet. Server process management is not yet automated — start the server process manually inside the VM to generate logs.

### Log file selector is empty

The log file selector populates from the server's log directory. If the server files haven't been installed yet, the directory is empty and nothing appears in the selector.

---

## Data Recovery

### Database appears corrupt or the app crashes on startup

Delete `%LOCALAPPDATA%\SietchConsole\sietch.db` and relaunch. The application will recreate the database and run migrations. **You will lose your saved profile and backup records.** Your configuration backup files in `%LOCALAPPDATA%\SietchConsole\Backups\` are unaffected.

### I accidentally deleted my battlegroup profile

Re-run the Setup Wizard. There is no undo for profile deletion in the current alpha.

---

## Collecting Logs for Bug Reports

When opening an issue, include:

1. The Sietch Console version (shown in the window title bar or About section).
2. Your Windows version (`winver` output).
3. The full diagnostics output (copy the text from the Diagnostics tab).
4. Any error messages shown in the application.
5. The application log file, if one was written (check `%LOCALAPPDATA%\SietchConsole\Logs\`).

Open issues at [github.com/michaelstoffer/sietch-console/issues](https://github.com/michaelstouffer/sietch-console/issues).
