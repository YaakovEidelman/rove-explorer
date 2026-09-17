# The Linux default-app claim

On Linux, Rove can become the system default for two separate things:

- The "Open File" and "Save File" dialogs other apps show — GTK, Qt,
  Flatpaks, anything that goes through `xdg-desktop-portal` — can open as
  Rove instead of GTK's or KDE's own picker.
- The default handler for `inode/directory`, which is what "Show in Folder"
  (Chrome downloads, most file-open buttons) and plain `xdg-open` on a
  directory use to decide which file manager to launch.

Windows has nothing like either; it is not touched by any of this document.

Three pieces make this work:

- `rove-portal`, a small headless program that speaks the
  `org.freedesktop.impl.portal.FileChooser` D-Bus interface and, when asked,
  opens `rove --picker` (see `docs/keybindings.md`) to do the actual picking.
- `xdg-desktop-portal` itself, which decides which installed backend
  implements which interface, and has to be told rove-portal exists and
  should be the one handling `FileChooser`.
- `~/.config/mimeapps.list`, the file every XDG-compliant desktop and
  `xdg-open` read to find the default app for a mime type — Rove's claim
  sets its `inode/directory` entry.

## What happens automatically, and what doesn't

Every launch, Rove copies `rove-portal` into `~/.local/lib/rove/` alongside
itself (see `docs/installing.md`) and writes the two files that tell
`xdg-desktop-portal` rove-portal exists and can handle `FileChooser`. This
only advertises that Rove is *available* as a backend — it never by itself
makes Rove the preferred one, and `rove-portal` does not touch preferences
either when `xdg-desktop-portal` activates it on demand.

Claiming either default only ever happens two ways:

1. **The first-run ask.** The first time Rove's main window opens and it
   finds neither default already pointed at itself, it asks once, in a
   confirm dialog, whether to become the default for both. Answering no
   still records that it asked — it will not ask again. Answering yes claims
   both, saving what was there before so the claim can be undone.
2. **The Settings row**, at any time after that — see below.

Nothing claims a default that already has an explicit owner (GTK's, KDE's,
another file manager's, or one set by hand) without you doing it yourself
from Settings.

## Where things go

- rove-portal itself: `~/.local/lib/rove/rove-portal`
- Portal advertisement:
  `~/.local/share/xdg-desktop-portal/portals/org.freedesktop.impl.portal.Rove.portal`
- D-Bus service activation:
  `~/.local/share/dbus-1/services/org.freedesktop.impl.portal.desktop.rove.service`
- The FileChooser preference: `~/.config/xdg-desktop-portal/portals.conf`
- What Rove found there before claiming it: `~/.local/state/rove/portal-install.json`
- The default file manager: `~/.config/mimeapps.list`'s `inode/directory` key
- What Rove found there before claiming it: `~/.local/state/rove/mime-default.json`
- Whether the first-run ask has happened: `~/.local/state/rove/portal-asked.json`

All of these follow `$XDG_DATA_HOME`/`$XDG_CONFIG_HOME`/`$XDG_STATE_HOME` when
set, falling back to the paths above.

## Settings

Rove's Settings screen has a "Default for opening files and folders" row
showing the current state: `Not installed`, `Rove`, or `Owned by something
else`. Activating it (Enter/Space, same as any other setting) does the
opposite of whatever it shows — claims both defaults if Rove doesn't have
them, or gives both back if Rove does.

Giving it back restores exactly what was there before Rove claimed it —
the other backend's preference and the other file manager's mime entry, or
nothing, whichever they were.

Uninstalling Rove (`rove --uninstall`, see `docs/installing.md`) also gives
back the claim if Rove is the one holding it.

## Known gap

Some desktops (GNOME among them) resolve "Show in Folder" via the
`org.freedesktop.FileManager1` D-Bus interface before ever consulting
`mimeapps.list`, and Nautilus registers that interface itself. Claiming the
`inode/directory` default fixes `xdg-open` and anything that reads
`mimeapps.list` directly; it will not override a desktop that goes straight
to `FileManager1`. Taking over that interface too is a larger, riskier
change — it means owning a D-Bus well-known name another running file
manager also claims — and hasn't been done here.

## Known limitations

The picker window isn't pinned above the window that asked for it on
Wayland — it works, but it can end up behind the calling app. This is a gap
in Avalonia.Wayland, not something Rove's own code can currently work around;
see the top of `src/Rove.Portal/TODO.md` for the details. It works correctly
on X11.
