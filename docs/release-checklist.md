# Release Checklist

Use this checklist before promoting a draft GitHub Release to published. Complete every item; note any exceptions.

---

## 1. Code Freeze

- [ ] All planned issues for this milestone are closed or explicitly deferred
- [ ] No uncommitted changes on `main`
- [ ] `Directory.Build.props` `<Version>` matches the tag being released
- [ ] `CHANGELOG.md` has an entry for this version with a release date

---

## 2. Build Validation

- [ ] `dotnet build -c Debug` succeeds with 0 errors, 0 warnings treated as errors
- [ ] `dotnet build -c Release` succeeds with 0 errors
- [ ] `dotnet publish -c Release -r win-x64 --self-contained` produces a single `.exe`
- [ ] Published `.exe` launches without errors on a clean Windows 10/11 machine

---

## 3. Functional Smoke Test

Run these manually on a machine with a real Hyper-V environment. VM control is now fully implemented — test against a real VM, not just UI flow.

**Dashboard**
- [ ] App opens and shows the Dashboard
- [ ] Start button provisions (if needed) and starts the VM; status transitions to Starting → Running
- [ ] Stop button shows confirmation overlay, then stops the VM; status transitions to Stopping → Offline
- [ ] Restart button stops then starts the VM; status transitions correctly end-to-end
- [ ] CPU% and RAM figures appear on the Dashboard card while the VM is Running
- [ ] Error banner appears if Hyper-V raises an error (e.g., access denied); Dismiss clears it
- [ ] Status bar at the bottom shows profile name and live status

**Setup Wizard**
- [ ] Wizard opens for a fresh profile
- [ ] All steps advance and validate correctly
- [ ] Completing the wizard saves a profile and returns to Dashboard

**Logs**
- [ ] Log view opens without errors
- [ ] Search and filter buttons respond correctly
- [ ] Empty state is shown when no log file is present

**Diagnostics**
- [ ] "Run Diagnostics" executes and results appear
- [ ] Pass / Warning / Failure badges are correct

**Backups**
- [ ] "Backup Configuration" creates a record in the list
- [ ] Empty state is shown when no backups exist
- [ ] Restore and Delete confirmation dialogs work
- [ ] "↑ Sync" button on a backup row uploads to the configured cloud provider
- [ ] Cloud Sync card: provider selector, credential fields, Test Connection returns success or a clear error
- [ ] Cloud Backups list populates after Refresh; ↓ Restore and Delete actions work
- [ ] S3 secret key is accepted via the PasswordBox and is not echoed in plain text

**Networking**
- [ ] Network Information card shows host IP (or a placeholder)
- [ ] Required Ports table is populated
- [ ] Firewall status check runs without crashing

**Settings**
- [ ] Settings loads existing config (or shows "no config" empty state)
- [ ] Saving settings triggers the auto-backup and shows a success message
- [ ] Remote Management card: enable toggle, port, token generate, Apply — web server starts/stops correctly
- [ ] Discord Webhooks card: URL field, Test button returns success or a descriptive error, Save persists settings
- [ ] Discord notification fires in the configured channel when the server starts or stops

**Players**
- [ ] Connected Players tab loads (empty state shown when server is not running)
- [ ] Ban List tab loads (empty state shown when no bans exist)
- [ ] Allowlist tab loads (empty state shown when allowlist is empty)
- [ ] Add to Allowlist form accepts a Steam ID and saves correctly
- [ ] Ban form opens with Permanent/timed options and saves a BanRecord
- [ ] Confirmation overlay appears before kick and unban actions

**Metrics**
- [ ] Metrics view opens without errors
- [ ] Time range selector (1 h, 6 h, 24 h, 7 d, 30 d) updates charts
- [ ] Charts are empty state when no data exists for the range
- [ ] Availability summary shows "No data" when no snapshots exist
- [ ] Live "Collecting" indicator appears when the server is running

---

## 4. Installer Validation

- [ ] Inno Setup script compiles without errors (`iscc sietch-console.iss`)
- [ ] Installer runs on a clean machine and installs to `Program Files`
- [ ] Start menu and optional desktop shortcut are created
- [ ] Application launches via Start menu shortcut
- [ ] Uninstaller removes the application cleanly
- [ ] `%LOCALAPPDATA%\SietchConsole\` is preserved after uninstall

---

## 5. Portable ZIP Validation

- [ ] ZIP extracts cleanly and the `.exe` runs from an arbitrary directory
- [ ] App data is written to `%LOCALAPPDATA%\SietchConsole\`, not next to the `.exe`

---

## 6. GitHub Release

- [ ] Tag pushed: `git tag v<version> && git push --tags`
- [ ] GitHub Actions `release.yml` workflow completed successfully
- [ ] Draft release contains both the portable ZIP and the Setup `.exe`
- [ ] Release notes are accurate (edit the auto-generated notes if needed)
- [ ] Release is marked **Pre-release** for alpha and beta builds
- [ ] Draft promoted to published

---

## 7. Post-Release

- [ ] `CHANGELOG.md` "Unreleased" section is clean for the next cycle
- [ ] A new `[Unreleased]` section is open in `CHANGELOG.md`
- [ ] Milestone is closed in GitHub
- [ ] Any critical issues found during release testing are filed as bugs
