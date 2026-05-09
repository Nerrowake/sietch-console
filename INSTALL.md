# Installing Sietch Console

## System Requirements

| Requirement | Minimum |
|-------------|---------|
| OS | Windows 10 20H1 (build 19041) or Windows 11 |
| Architecture | x64 |
| RAM | 8 GB (16 GB recommended for running the VM simultaneously) |
| Disk | 500 MB free for the application; additional space required for the Dune: Awakening server files |
| Hyper-V | Must be enabled in Windows Features |
| .NET runtime | Not required — the installer includes a self-contained runtime |

> **Note:** Sietch Console manages a Dune: Awakening battlegroup running inside a Hyper-V virtual machine. Hyper-V requires Windows 10/11 **Pro, Enterprise, or Education** — it is not available on Windows Home.

---

## Installation (Installer)

1. Download `SietchConsole-<version>-Setup.exe` from the [Releases page](https://github.com/michaelstouffer/sietch-console/releases).
2. Run the installer. Windows may show a SmartScreen prompt — click **More info → Run anyway** (the binary is not yet code-signed).
3. Follow the on-screen setup wizard.
4. Launch **Sietch Console** from the Start menu or desktop shortcut.

---

## Installation (Portable ZIP)

If you prefer not to use the installer:

1. Download `SietchConsole-<version>-win-x64-portable.zip` from the [Releases page](https://github.com/michaelstouffer/sietch-console/releases).
2. Extract the ZIP to any folder (e.g. `C:\Tools\SietchConsole\`).
3. Run `Sietch Console.exe` directly.

Application data is stored in `%LOCALAPPDATA%\SietchConsole\` — not in the application folder — so the portable version can be placed anywhere.

---

## First Launch

On first launch, Sietch Console will open the **Setup Wizard**. The wizard will:

1. Verify your system meets the requirements (Hyper-V, memory, disk).
2. Ask for the path to your Dune: Awakening server install directory.
3. Ask for your Funcom authentication token.
4. Configure the Hyper-V virtual machine settings.
5. Configure your battlegroup (server name, passwords, player limits).

Complete all steps to create your battlegroup profile. You can re-run diagnostics or change settings at any time after setup.

---

## Uninstalling

**Installer version:** Use **Add or Remove Programs** → search for **Sietch Console** → **Uninstall**.

**Portable version:** Delete the application folder.

In both cases, your battlegroup profile and backup data in `%LOCALAPPDATA%\SietchConsole\` are **not** automatically deleted. Remove that folder manually if you want a complete uninstall.

---

## Troubleshooting

**The application won't start after installation.**
Confirm your system is running Windows 10 build 19041 or later (`winver`). The self-contained build requires x64 architecture.

**Hyper-V checks fail in the Setup Wizard.**
Open **Windows Features** (`optionalfeatures.exe`), enable **Hyper-V**, and restart your machine. Hyper-V requires a Pro/Enterprise/Education edition of Windows.

**SmartScreen blocks the installer.**
Click **More info → Run anyway**. The binary is unsigned in early alpha builds. This warning will be resolved when code signing is added.

**Firewall rules aren't being created.**
The firewall rule creator requires administrator privileges. When prompted, approve the UAC elevation dialog.

For additional help, open an issue at [github.com/michaelstoffer/sietch-console/issues](https://github.com/michaelstoffer/sietch-console/issues).
