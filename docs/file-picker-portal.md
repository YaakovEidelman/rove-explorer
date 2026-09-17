# The Linux file picker portal

On Linux, Rove can act as the system file picker: the "Open File" and "Save
File" dialogs other apps show — GTK, Qt, Flatpaks, anything that goes through
`xdg-desktop-portal` — can open as Rove instead of GTK's or KDE's own picker.
Windows has nothing like this; it is not touched by any of this document.

Two pieces make this work:

- `rove-portal`, a small headless program that speaks the
  `org.freedesktop.impl.portal.FileChooser` D-Bus interface and, when asked,
  opens `rove --picker` (see `docs/keybindings.md`) to do the actual picking.
- `xdg-desktop-portal` itself, which decides which installed backend
  implements which interface, and has to be told rove-portal exists and
  should be the one handling `FileChooser`.

## What happens automatically

The first time Rove runs, and every time after that, it:

1. Copies `rove-portal` into `~/.local/lib/rove/` alongside itself, the same
   place its own binary lives (see `docs/installing.md`).
2. Writes the two files that tell `xdg-desktop-portal` rove-portal exists and
   can handle `FileChooser` — this always happens, whether or not rove-portal
   ends up the one actually in charge.
3. Claims the `FileChooser` preference for itself, but only the first time it
   finds no preference there at all. If a preference already exists — GTK's,
   KDE's, another file manager's, or one you set by hand — Rove leaves it
   alone. What it found (or that it found nothing) is saved first, so the
   claim can be undone later.

None of this needs a display or a running `rove-portal` process — Rove does
the registration itself, in the background, on every launch. `xdg-desktop-portal`
starts `rove-portal` on its own, on demand, the first time something asks it
to open a file.

## Where things go

- rove-portal itself: `~/.local/lib/rove/rove-portal`
- Portal advertisement:
  `~/.local/share/xdg-desktop-portal/portals/org.freedesktop.impl.portal.Rove.portal`
- D-Bus service activation:
  `~/.local/share/dbus-1/services/org.freedesktop.impl.portal.desktop.rove.service`
- The FileChooser preference: `~/.config/xdg-desktop-portal/portals.conf`
- What Rove found there before claiming it: `~/.local/state/rove/portal-install.json`

All of these follow `$XDG_DATA_HOME`/`$XDG_CONFIG_HOME`/`$XDG_STATE_HOME` when
set, falling back to the paths above.

## Settings

Rove's Settings screen has a "Linux file picker" row showing the current
state: `Not installed`, `Rove`, or `Owned by something else`. Activating it
(Enter/Space, same as any other setting) does the opposite of whatever it
shows — claims the preference if Rove doesn't have it, or gives it back if
Rove does. This is the same "ask first" step the automatic claim skips when
something else already has an opinion: nothing takes over a preference you
didn't already have set for yourself without you doing it from here.

Giving it back restores exactly what was there before Rove claimed it — the
other backend's preference, or nothing, whichever it was.

Uninstalling Rove (`rove --uninstall`, see `docs/installing.md`) also gives
back the claim if Rove is the one holding it.

## Known limitations

The picker window isn't pinned above the window that asked for it on
Wayland — it works, but it can end up behind the calling app. This is a gap
in Avalonia.Wayland, not something Rove's own code can currently work around;
see the top of `src/Rove.Portal/TODO.md` for the details. It works correctly
on X11.
