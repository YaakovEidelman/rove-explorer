# TODO — Wayland-first windowing

Rove runs as a native Wayland client (`Program.cs` calls
`UseWaylandWithFallback()` after `UsePlatformDetect()`), confirmed via
`hyprctl clients` reporting `xwayland: false` for its window on a real
Hyprland session.

Manually verified on that session, all correct: HiDPI/fractional scaling
(tested at 1.5x monitor scale), the self-install/update flow
(`DesktopInstall`, via `--install`/`--uninstall` against a scratch `$HOME`),
and the `--picker` launch mode used by `rove-portal`. Clipboard was
confirmed working in an earlier pass (copy/paste of files, both within Rove
and round-tripped through `wl-copy`/`wl-paste`, plus text copy).

Known issue, **not blocking release**: a window opened under native Wayland
reports an empty `app_id` (`hyprctl clients` shows `class: ""` for it),
because `Avalonia.Wayland` 12.1.2's `WaylandPlatformOptions` has no way to
set one — unlike `X11PlatformOptions.WmClass`, which is set to `"rove"` in
`BuildAvaloniaApp()` and only applies under X11/XWayland. This means
`StartupWMClass=rove` in the installed `.desktop` entry (see
`LinuxInstall.cs`) can't match under native Wayland, so taskbar/dock icon
grouping may not work there. The app still runs and installs fine either
way. Fix is upstream: AvaloniaUI/Avalonia PR #22209 (open as of 2026-09-16,
adds `WaylandPlatformOptions.AppId`; a maintainer mentioned a possible
12.1.x backport). Once released, set `AppId = "rove"` next to the X11
`WmClass` in `Program.cs` and delete this note.

The portal's parent_window correlation on native Wayland is a separate,
also-non-blocking gap — tracked in detail in src/Rove.Portal/TODO.md.
