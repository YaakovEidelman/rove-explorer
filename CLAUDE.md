# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

Rove is a keyboard-first, cross-platform (Windows/Linux) file manager built with
.NET 10 and Avalonia. It compiles to native code ahead-of-time (AOT) — no
runtime to install, fast startup.

## Commands

Requires the .NET 10 SDK.

All helper scripts live in `scripts/` and work from any directory. **Always use them instead of
raw `dotnet` commands.** They pass `-p:RuntimeIdentifier=linux-x64` (a plain `dotnet test` or
`dotnet build` fails on this machine's `arch-x64` default RID) and print only the summary and
failures, which keeps output short.

```
# Run the app (Linux)
scripts/run.sh

# Run the picker window on its own (Linux)
scripts/run_picker.sh

# Run all tests (prints one summary line per project, plus failure details)
scripts/test.sh

# Run some tests: each argument is a class or test name to match
scripts/test.sh PathGuardTests TabKeyTests

# Build the app and portal (prints only errors and warnings)
scripts/build.sh

# Delete a finished TODO.md item, renumber, and stage only that change
scripts/todo-done.sh "text the item starts with"

# Publish a release build (AOT, native)
dotnet publish src/Rove.UI/Rove.UI.csproj -c Release -r <win-x64|linux-x64>
```

When you do something often by hand, add a script for it in `scripts/`.

`Rove.slnx` is the solution file (the new XML-based slnx format, not `.sln`).

CI has three workflows. `.github/workflows/build.yml` is the reusable job
that tests and publishes win-x64 and linux-x64 separately — native AOT
compiles on the platform it targets, no cross-compiling — and is called by
the other two rather than duplicated. `dev-build.yml` (manual-trigger only,
`workflow_dispatch`) calls it and replaces a single rolling `dev`
prerelease. `release.yml` (triggered by a pushed `vX.Y.Z` tag) calls it and
creates a permanent, versioned GitHub release with generated notes — the
stable channel. For day-to-day solo dev, skip CI and run
`scripts/dev-release-linux.sh` (Linux) or `scripts/dev-release-win.ps1` (Windows)
instead — each tests, publishes, and uploads its own platform's asset to
the `dev` release directly from a local machine.

## Architecture

### Core / UI split

- **`src/Rove.Core`** — platform-agnostic backend: filesystem operations, archive
  browsing, trash, search, theming data. No Avalonia dependency. Built as an
  `IsAotCompatible` library.
- **`src/Rove.UI`** — Avalonia MVVM frontend (Views/ViewModels), plus
  UI-adjacent services (install/self-update, keymap, clipboard, icon cache).

`RoveCore` (`src/Rove.Core/RoveCore.cs`) is the facade the UI talks to directly
—`Actions`, `GlobalSearchService`, per-view `FileWatchService` instances. In
parallel, `Protocol/Dispatcher.cs` wraps the same `Actions`/`GlobalSearchService`
methods behind a verb-name → JSON-envelope router (`RequestEnvelope`/
`ResponseEnvelope`, source-generated `ProtocolJson` serialization for AOT).
The UI does not go through the dispatcher today — it exists so the backend
can be moved out-of-process later without touching handler code. When adding
a new backend operation, add it to `Actions` and register it in both the UI
call site and `Dispatcher`'s constructor if it's meant to be reachable that
way.

### ContentViewModel

`src/Rove.UI/ViewModels/ContentViewModel.cs` is the single-pane browse-mode
ViewModel — the busiest command surface in the UI — split across one core
file and same-class partials by concern, all `public partial class
ContentViewModel`: `ContentViewModel.cs` (constructor, shared fields,
`Targets()`/`RefusedInArchive()`, `RegisterBindings()`), `.Navigation.cs`
(directory navigation, history, archive-as-folder, drives), `.PathBar.cs`
(path bar edit/completion), `.View.cs` (view mode, icon size, columns, local
search, hidden items, marks), `.Editing.cs` (rename, create), `.FileOps.cs`
(delete, trash, clipboard, extract, compress), `.Bookmarks.cs`, `.Undo.cs`.
Add a new browse-mode verb to whichever partial matches its concern, then
wire it into `RegisterBindings()` in the core file.

### Command / keybinding system

User input flows through one indirection layer, documented in full in
`docs/keybindings.md`:

- `CommandDef` (`src/Rove.UI/Services/CommandRegistry.cs`) — the fixed set of
  named commands (e.g. `content.move_down`, `palette.toggle`), each tagged
  `System` (plumbing, hidden from the palette) or `User` (shows in the command
  palette).
- `KeymapConfig`/`KeymapDefaults` — maps a `KeyStroke` to a `CommandDef.Id`
  per **mode** (`browse`, `palette`, `localsearch`, `renameitem`, etc. — see
  the mode table in `docs/keybindings.md`). User overrides load from
  `keybindings.json` and merge over the built-in defaults; assigning a
  command a new key moves it there rather than adding a binding.
- ViewModels register their commands' `Action` bodies with `CommandRegistry`;
  the registry resolves `key + mode → command → action`.

Adding a new user-facing action means: add a `CommandDef`, give it a default
key in `KeymapDefaults`, wire the `Action` in the owning ViewModel, and
document it in `docs/keybindings.md`.

### Platform abstraction

Windows/Linux differences are split into paired files rather than `#if`
blocks: `WindowsTrash`/`LinuxTrash`, `WindowsInstall`/`LinuxInstall`, and an
`IIconFetcher` interface with a `IconFetcherChooser.CreateForHost()` factory.
Follow this pattern for new platform-specific behavior — a small interface or
static chooser, one implementation file per OS.

### Self-install/update

`src/Rove.UI/Services/DesktopInstall.cs` and friends (`InstallRecord`,
`SelfDelete`) implement Rove's copy-itself-in-and-self-update behavior on
first run, described end-to-end in `docs/installing.md`. `ROVE_NO_INSTALL=1`
disables it for local dev runs.

### Linux file picker portal

`src/Rove.Portal` is `rove-portal`, a headless D-Bus service implementing
`org.freedesktop.impl.portal.FileChooser`, letting other apps' "Open File"
dialogs open as Rove (`rove --picker`) on Linux. The install/backup/revert
logic it and `Rove.UI`'s Settings screen share lives in `Rove.Core.Services`
(`PortalFiles`, `PortalInstall`), wrapped for `Rove.UI` by
`FilePickerPortal`. Described end-to-end in `docs/file-picker-portal.md`.

### AOT constraints

`Rove.UI.csproj` has `PublishAot`, `EnableAotAnalyzer`, `EnableTrimAnalyzer`,
and `EnableSingleFileAnalyzer` all on, and `Rove.Core.csproj` sets
`IsAotCompatible`. Avoid reflection-based serialization (JSON uses
source-generated `JsonTypeInfo<T>` via `ProtocolJson`, not
`JsonSerializer.Deserialize<T>()` directly) and anything else the trim/AOT
analyzers would flag — these are checked at publish time, not just at
compile time of a `dotnet build`.

### Testing

- `tests/Rove.Core.Tests` — plain xUnit against `Rove.Core` logic.
- `tests/Rove.UI.Tests` — xUnit + `Avalonia.Headless`, driving a **real**
  `MainWindow` on a windowless backend (`HeadlessSession.cs`). `WindowHarness`
  (`tests/Rove.UI.Tests/WindowHarness.cs`) opens a real window against a
  scratch temp folder, wires real ViewModels/services (with fakes only for
  the OS clipboard and image preview loader), and drives it with
  `Press(Key)`/`Settle()` so key-routing tests exercise the same path a real
  keypress takes. Prefer extending `WindowHarness` over mocking ViewModels
  when testing keyboard behavior.
- `tests/Rove.Portal.Tests` — xUnit against `Rove.Portal`, Linux-only (see
  `src/Rove.Portal/TODO.md`). Skipped on Windows CI (`build.yml`'s windows
  job runs only the other two `dotnet test` invocations directly); included
  when running the full `Rove.slnx` solution.
