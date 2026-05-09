# Contributing to Sietch Console

Thank you for taking the time to contribute. This document covers everything you need to know to submit issues, open pull requests, and write code that fits the project's standards.

---

## Getting Started

**Prerequisites**

- Windows 10/11 with Hyper-V enabled (Pro, Enterprise, or Education)
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

---

## Branch Naming

All work branches off `development`. Branch names follow this pattern:

| Type | Format | Example |
|------|--------|---------|
| Feature | `feature/short-description` | `feature/log-export` |
| Bug fix | `fix/short-description` | `fix/firewall-check-crash` |
| Documentation | `docs/short-description` | `docs/setup-guide` |
| Refactor | `refactor/short-description` | `refactor/diagnostics-service` |
| Release prep | `release/v0.x.y` | `release/v0.2.0` |

Use lowercase and hyphens only. Keep the description short (3–5 words).

---

## Commit Messages

Write commit messages in the imperative mood, present tense:

```
Add firewall rule auto-creator
Fix log streaming crash on missing file
Update backup restore confirmation dialog
```

Keep the subject line under 72 characters. Use the body for context when the reason is not obvious — do not describe what the diff shows, only why the change was made.

---

## Pull Request Process

1. **One issue per PR.** If a PR covers multiple issues, split it.
2. **Target `development`,** not `main`. PRs to `main` will be closed.
3. **Title your PR** with the same imperative format as commit messages.
4. **Reference the issue** in the PR body: `Closes #42`.
5. **Keep the diff focused.** Do not include unrelated cleanup or reformatting.
6. **Self-review before requesting review.** Read your own diff, build and run the app, and smoke-test the affected views.

---

## Issue Workflow

- **Bug reports:** Use the Bug report template. Include repro steps, expected vs. actual behavior, and any relevant log output.
- **Feature requests:** Use the Feature request template. Explain the problem you are solving, not just the solution you want.
- **Milestone assignment:** Issues are grouped into numbered milestones. The milestone description lists the goal and acceptance criteria for that batch of work.
- **Labels:** Use the existing labels — `bug`, `enhancement`, `docs`, `blocked`, `good first issue`. Do not create new labels without discussion.

---

## Coding Standards

### General

- Target `net8.0-windows` in the WPF project; `net8.0` in Core and Data.
- All new code must compile with zero errors and zero warnings treated as errors (`TreatWarningsAsErrors` is set in `Directory.Build.props`).
- Nullable reference types are enabled (`<Nullable>enable</Nullable>`). Never use `!` (null-forgiving operator) without a comment explaining why null is impossible at that point.
- Implicit usings are enabled. Do not add redundant `using` directives for `System`, `System.Collections.Generic`, etc.

### ViewModels

- Extend `ObservableObject` from `CommunityToolkit.Mvvm`.
- Use `[ObservableProperty]` for all bindable properties (generates the backing field, property, and change notification).
- Use `[RelayCommand]` for all commands (generates the `ICommand` property).
- Every ViewModel that performs async work must expose `IsLoading` (or `IsRunning` for control operations) and bind it to `LoadingBarStyle`.
- Never call OS APIs, file I/O, or network operations directly from a ViewModel — always go through a service interface.

### Services

- Implement the corresponding `Core/Interfaces/I{Name}Service` interface.
- Register services in `App.xaml.cs`.
- Use `async`/`await` for all I/O. Do not use `Task.Run` to wrap synchronous code unless there is no async alternative.
- Services must not hold UI references or raise UI events.

### XAML / Views

- **No inline color values** — always reference a named brush from `SietchTheme.xaml`.
- **No inline font sizes, families, or corner radii** — use the named typography and corner radius resources.
- Use named button styles (`PrimaryButtonStyle`, `DangerButtonStyle`, etc.) — do not create per-view button styles.
- Every view with a list or content area must include an empty state using `EmptyStateBorderStyle` / `EmptyStateHeadingStyle` / `EmptyStateSubtextStyle`.
- Every view with async operations must include a `LoadingBarStyle` ProgressBar bound to `IsLoading`.
- Code-behind (`.xaml.cs`) should contain only event handlers that cannot be expressed as commands (e.g., `ScrollChanged`, element focus). All logic belongs in the ViewModel.

### Models

- Domain models in `SietchConsole.Core/Models/` are `sealed record` types.
- They carry data only — no methods, no service calls, no side effects.
- EF Core entity classes in `SietchConsole.Data/Entities/` mirror the Core models and add only persistence attributes.

### Database

- All schema changes require a new EF Core migration: `dotnet ef migrations add <Name> --project SietchConsole.Data`.
- Never edit migration files by hand after they have been pushed.
- Repositories must be accessed via `IServiceScopeFactory` in ViewModels — do not inject `IRepository<T>` directly as a singleton.

---

## Versioning

The version is set in `Directory.Build.props`. Do not set `<Version>` in individual `.csproj` files. When preparing a release, update the version there, update `CHANGELOG.md`, and push a matching tag.

---

## Documentation

When adding a new feature:

- Update `CHANGELOG.md` (under `[Unreleased]`) with a brief entry in the appropriate section (Added / Changed / Fixed / Removed).
- If the feature changes user-facing behavior documented in `INSTALL.md`, `docs/user-setup-guide.md`, or `docs/troubleshooting.md`, update those files in the same PR.
- If the feature adds a new service or significantly changes architecture, update `docs/architecture.md`.

---

## What Not to Do

- Do not add `NuGet` packages without discussion. The package list is intentionally small.
- Do not create helper utilities, extension methods, or abstractions unless they are used in at least two places.
- Do not add comments that describe what the code does — only add a comment when the **why** is non-obvious.
- Do not use `MessageBox.Show` — the app has an established overlay/confirmation pattern.
- Do not commit to `main` directly. `main` is reserved for stable release snapshots.
