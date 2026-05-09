# CLAUDE.md — Sietch Console AI Development Guide

This file is automatically loaded by Claude Code. It describes the architecture, conventions, and constraints that govern this codebase.

---

## Project Identity

**Sietch Console** is a Windows-native WPF (.NET 8) desktop application for self-hosting Dune: Awakening dedicated servers via Hyper-V. It is a personal community project — not affiliated with Funcom.

Primary working directory: `C:\FS\Sietch Console`
Solution file: `Sietch Console.sln`

---

## Solution Structure

```
Sietch Console.sln
├── Sietch Console/          WPF app — views, viewmodels, services, theme
├── SietchConsole.Core/      Domain models and service interfaces (no dependencies)
└── SietchConsole.Data/      EF Core + SQLite repositories
```

**Dependency rule:** `Core` has zero project references. `Data` references `Core`. The WPF app (`Sietch Console`) references both. Never create a circular dependency or add project references to `Core`.

---

## Architecture

**Pattern:** MVVM with `Microsoft.Extensions.Hosting` for DI and startup.

- All ViewModels are registered as **singletons** in `App.xaml.cs`.
- Repositories are **scoped** and accessed via `IServiceScopeFactory` from within ViewModels.
- Services are registered as **singletons** unless they hold per-request state.
- `MainWindow` and `MainWindowViewModel` own navigation. Views are instantiated once and swapped via `ContentControl` binding.

**Key service namespaces:**

| Namespace | Responsibility |
|-----------|---------------|
| `Services/Control/` | Start/stop/restart the Hyper-V VM and server process |
| `Services/Configuration/` | Read and write INI files; auto-backup before save |
| `Services/Diagnostics/` | System readiness checks (Hyper-V, memory, disk, network, firewall) |
| `Services/Logs/` | Tail log files; analyze entries for detected issues |
| `Services/Backups/` | Create, restore, and delete backup records |
| `Services/Networking/` | Detect IPs, check firewall rules, test ports |
| `Services/Installation/` | Setup wizard orchestration (Steam detection, package download, script execution) |

---

## Theme System

All visual tokens live in `Sietch Console/Themes/SietchTheme.xaml`, which is merged in `App.xaml`. **Never hardcode colors, font sizes, or corner radii inline** — always reference a named resource.

### Key brushes

| Key | Color | Use |
|-----|-------|-----|
| `AppBackground` | `#0B0F14` | Main window and view backgrounds |
| `PanelBackground` | `#141A22` | Panels, cards, sidebar |
| `SurfaceBackground` | `#111820` | Inner surfaces, log stream header |
| `CardBackground` | `#1A212B` | Elevated card surfaces |
| `FieldBackground` | `#0F151C` | Input fields |
| `AccentPrimary` | `#C46A2B` | Primary actions, active states |
| `AccentSandstone` | `#D6B98C` | Headings, wordmark |
| `AccentAmber` | `#E0A64B` | Warnings, attention |
| `AccentCyan` | `#4FB3B8` | Info, networking |
| `AccentTeal` | `#1F6F73` | Secondary actions |
| `TextPrimary` | `#F3EBDD` | Primary readable text |
| `TextSecondary` | `#B8AA92` | Body text |
| `TextMuted` | `#7B6F5D` | Labels, captions |

### Key styles

| Key | Type | Use |
|-----|------|-----|
| `CardStyle` | `Border` | Standard card container |
| `PrimaryButtonStyle` | `Button` | Main actions |
| `TealButtonStyle` | `Button` | Secondary actions |
| `DangerButtonStyle` | `Button` | Destructive actions |
| `CancelButtonStyle` | `Button` | Cancel/dismiss |
| `SuccessButtonStyle` | `Button` | Positive/create actions |
| `InfoButtonStyle` | `Button` | Informational actions |
| `LoadingBarStyle` | `ProgressBar` | Indeterminate loading strip (Height=2, AccentPrimary) |
| `EmptyStateBorderStyle` | `Border` | Empty state outer border |
| `EmptyStateHeadingStyle` | `TextBlock` | Empty state heading |
| `EmptyStateSubtextStyle` | `TextBlock` | Empty state body copy |

### Empty state pattern

Every view with a list or content area must show a standardized empty state when there is nothing to display:

```xml
<Border Style="{StaticResource CardStyle}" Padding="20,16"
        Visibility="{Binding HasItems, Converter={StaticResource BoolToCollapsedConverter}}">
    <Border Style="{StaticResource EmptyStateBorderStyle}">
        <StackPanel HorizontalAlignment="Center">
            <TextBlock Style="{StaticResource EmptyStateHeadingStyle}"
                       Text="Nothing here yet." Margin="0,0,0,6" />
            <TextBlock Style="{StaticResource EmptyStateSubtextStyle}"
                       Text="Explain what the user should do." MaxWidth="420" />
        </StackPanel>
    </Border>
</Border>
```

### Loading bar pattern

Any view that performs async work must show a 2px indeterminate progress bar at the top of its content area while loading:

```xml
<ProgressBar Style="{StaticResource LoadingBarStyle}"
             Visibility="{Binding IsLoading, Converter={StaticResource BoolToVisibleConverter}}"
             Margin="0,0,0,16" />
```

---

## Value Converters

All converters are registered in `SietchTheme.xaml` and available in every view without a local resource declaration:

| Key | Converts |
|-----|---------|
| `BoolToVisibleConverter` | `true` → `Visible`, `false` → `Collapsed` |
| `BoolToCollapsedConverter` | `true` → `Collapsed`, `false` → `Visible` (inverted) |
| `BoolInverseConverter` | `true` → `false`, `false` → `true` |
| `NullToCollapsedConverter` | `null` → `Collapsed`, non-null → `Visible` |
| `PathToFileNameConverter` | Full path → filename only |
| `FileSizeConverter` | Long bytes → human-readable string |

---

## Naming Conventions

- **ViewModels:** `{Feature}ViewModel` — e.g., `BackupsViewModel`
- **Views:** `{Feature}View` — e.g., `BackupsView.xaml`
- **Services:** `{Feature}Service` — e.g., `BackupService`
- **Interfaces:** `I{ServiceName}` — e.g., `IBackupService`
- **Models:** noun records — e.g., `BackupRecord`, `BattlegroupProfile`
- **Commands:** `{Verb}{Noun}Command` — e.g., `CreateBackupCommand`
- **Step ViewModels:** `{StepName}StepViewModel` in `ViewModels/Steps/`
- **Diagnostic checks:** `{What}Check` implementing the check interface

---

## Coding Standards

- Use `CommunityToolkit.Mvvm` for all ViewModel code: `[ObservableProperty]`, `[RelayCommand]`, `ObservableObject`.
- All ViewModels must have `IsLoading` (or `IsRunning` for control operations) bound to `LoadingBarStyle`.
- Use `async`/`await` throughout — never block the UI thread.
- Prefer `sealed record` for domain models in `SietchConsole.Core/Models/`.
- Repository access from ViewModels: always open a scope via `IServiceScopeFactory`, not a bare `IRepository<T>`.
- Do not call OS APIs directly in ViewModels — go through a service interface.
- No `MessageBox.Show` — use the overlay/confirmation pattern already established in the app.

---

## Versioning

Version is set once in `Directory.Build.props` at the repo root. All three projects inherit it. Do not set `<Version>` in individual `.csproj` files.

Strategy:
- `0.x.y-alpha.N` — internal alpha (current phase)
- `0.x.y-beta.N` — wider test builds
- `x.y.z` — stable release

To release: update `<Version>` in `Directory.Build.props`, update `CHANGELOG.md`, commit, and push a `v*.*.*` tag. GitHub Actions handles build and release.

---

## GitHub Workflow

- Default branch: `development`
- Branch naming: `feature/short-description`, `fix/short-description`, `docs/short-description`
- All work targets the `development` branch; `main` is reserved for stable releases
- Issues are tracked with milestone labels (Milestone 1, Milestone 2, …)
- Close issues with `gh issue close <number> --repo michaelstoffer/sietch-console`

---

## Files Not to Touch Without Care

| File | Why |
|------|-----|
| `Themes/SietchTheme.xaml` | Single source of truth for all visual tokens; changes cascade everywhere |
| `App.xaml.cs` | DI registration and startup; adding services here requires understanding singleton vs. scoped lifetime |
| `Directory.Build.props` | Version and assembly metadata for all projects |
| `.github/workflows/release.yml` | Tag-triggered CI; changes break the release pipeline |
| `Installer/sietch-console.iss` | Inno Setup; requires Inno Setup 6.x to test locally |

---

## What Does Not Exist Yet (Alpha Gaps)

- No real Hyper-V integration — VM control stubs return mock state
- No actual server process management — `BattlegroupControlService` is a shell
- No SteamCMD integration — `ServerPackageService` is a stub
- The `.ico` app icon (`assets/sietch-console.ico`) has not been created yet
- No code signing — SmartScreen will warn on the installer
- No auto-update mechanism

Do not add production-quality implementations of these unless the corresponding issue is in scope for the current milestone.
