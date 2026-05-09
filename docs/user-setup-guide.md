# Setting Up Your Sietch Battlegroup

This guide walks you through setting up Sietch Console and getting a Dune: Awakening dedicated server running for the first time. No technical experience is required.

---

## Before You Begin

Make sure your PC meets these requirements:

| Requirement | Details |
|-------------|---------|
| Windows edition | Windows 10 or 11 — **Pro, Enterprise, or Education only** (not Home) |
| Windows version | Build 19041 or later — press `Win + R`, type `winver`, and press Enter to check |
| RAM | At least 8 GB (16 GB recommended when running the server VM at the same time) |
| Disk space | At least 500 MB for Sietch Console, plus space for the Dune: Awakening server files |
| Hyper-V | Must be enabled (see below if it isn't) |

> **Why not Windows Home?** Hyper-V — the built-in Windows virtualization technology that runs the game server — is only available on Pro and higher editions. Sietch Console cannot work without it.

---

## Step 1 — Enable Hyper-V (if needed)

If you have already enabled Hyper-V, skip to Step 2.

1. Press `Win + R`, type `optionalfeatures`, and press Enter.
2. In the **Windows Features** window, check **Hyper-V** and click **OK**.
3. Windows will install the required components and ask you to restart.
4. After restarting, Hyper-V is ready.

---

## Step 2 — Install Sietch Console

1. Download the latest release from the [Releases page](https://github.com/michaelstoffer/sietch-console/releases).
2. Run `SietchConsole-<version>-Setup.exe`.
3. If Windows SmartScreen shows a warning, click **More info** → **Run anyway**. (The installer is not yet code-signed — this warning is expected in early builds.)
4. Follow the on-screen steps and click **Install**.
5. Launch **Sietch Console** from the Start menu.

---

## Step 3 — Run the Setup Wizard

The Setup Wizard opens automatically the first time you launch Sietch Console. It has eight steps.

### Welcome

This is just an introduction. Click **Next** to begin.

### Requirements Check

Sietch Console checks your system automatically:

- **Green (Pass):** This requirement is met.
- **Yellow (Warning):** The check passed with a note — read the description.
- **Red (Failure):** Something needs to be fixed before you can continue.

If a check fails, click on it to see a plain-English explanation and what to do. Fix the issue, then click **Re-check** to try again. You must pass all required checks to proceed.

### Install Path

Enter the path where you want the Dune: Awakening server files to be installed. This is where SteamCMD will download the server.

- Click the folder icon to browse.
- Choose a drive with plenty of free space — the server files are several gigabytes.
- A folder named `DuneAwakening` will be created inside the path you choose.

### Funcom Token

Enter your Funcom authentication token. This is required to download and run a Dune: Awakening dedicated server.

- Your token is stored locally in the encrypted SQLite database. It is never sent to any external service by Sietch Console.
- If you do not have a token yet, follow the instructions on the Funcom developer portal to obtain one.

### VM Configuration

Configure the Hyper-V virtual machine that will run the game server:

- **VM Name** — the name shown in Hyper-V Manager (default: `SietchServer`)
- **CPU cores** — how many CPU cores to allocate to the VM (1–8)
- **Memory** — how much RAM to allocate (minimum 4 GB)
- **Virtual switch** — the Hyper-V virtual switch that connects the VM to your network

If you are not sure, leave the defaults. You can change these settings later from the **Settings** view.

### Battlegroup Config

Set the visible identity of your server:

- **Battlegroup name** — the name players will see in the server browser
- **MOTD (Message of the Day)** — shown to players when they join (optional)
- **Max players** — how many players can be in the server at once
- **Server password** — leave blank for a public server; set a password to restrict access
- **Admin password** — used to grant yourself admin commands in-game

### Review

A summary of everything you configured. Read through it and confirm the details are correct. Click **Back** to go back and change anything.

### Progress

Sietch Console performs the initial setup: configuring the Hyper-V VM, downloading the server files, and writing the configuration. This may take several minutes depending on your internet connection.

A progress bar and status messages show what is happening. Do not close the window during this step.

When the progress bar completes and the status shows **Setup complete**, click **Finish**. You are taken to the Dashboard.

---

## Step 4 — Start Your Server

1. On the **Dashboard**, click the **Start** button.
2. A confirmation prompt appears — click **Confirm** to proceed.
3. The status dot changes to **Starting**, then **Running** when the server is ready.
4. Players can now connect using the details on the **Networking** tab.

---

## Sharing Your Server Details

Go to the **Networking** tab and click **Copy Connection Summary**. This gives you a plain-text block with your server IP, port, and name that you can paste into Discord or any chat.

> Your server must be reachable on the internet for players outside your local network to connect. See the port forwarding guide in the **Networking** tab for instructions specific to your router.

---

## Stopping the Server

On the **Dashboard**, click **Stop**. Confirm the prompt. The status changes to **Stopping**, then **Offline**. The server is stopped cleanly — no data is lost.

---

## Changing Settings After Setup

- **Server identity and gameplay settings** — use the **Settings** view. Changes are backed up automatically before saving.
- **VM memory and CPU** — also in the **Settings** view, under VM Configuration.
- **Battlegroup name, passwords, player limits** — in Settings, under Battlegroup Config.

After saving any setting that requires a server restart, stop and restart the server from the **Dashboard** for the changes to take effect.

---

## Frequently Asked Questions

**Can I move the server files to a different drive after setup?**
Not without re-running the wizard. The install path is baked into the battlegroup profile and the VM configuration.

**Can I run multiple battlegroups?**
Not in the current alpha. Only one battlegroup profile is supported at a time.

**The Setup Wizard skipped the download step — did something go wrong?**
The download step is currently a stub in the alpha build. The wizard completes successfully, but you will need to install the server files manually. Future builds will automate this.

**My server shows as Offline even after clicking Start.**
This is expected behavior in the alpha — the Hyper-V and process control integration is not yet complete. The Dashboard shows the UI for server control but the underlying VM management is not hooked up yet.

---

## Getting Help

If you run into a problem not covered here:

1. Check the **Diagnostics** tab — click **Run Diagnostics** to see a full system readiness report.
2. Check the **Logs** tab for error messages from the server process.
3. Open an issue at [github.com/michaelstoffer/sietch-console/issues](https://github.com/michaelstoffer/sietch-console/issues).
