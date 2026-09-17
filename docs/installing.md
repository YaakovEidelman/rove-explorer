# Installing

Download Rove, unpack it, and run it. That is the whole procedure — there is
no script to run and nothing to put anywhere by hand.

A download is a small folder rather than one file: the program, and the two or
three libraries it draws with. Keep them together — running the program is what
installs the set.

The first time it starts, Rove copies itself to where the operating system
keeps programs and tells the desktop it exists. After that it is in the Start
Menu or the app grid like anything else, with its own icon, and you can delete
what you downloaded.

## Where things go

| | Windows | Linux |
| --- | --- | --- |
| The program | `%LOCALAPPDATA%\Programs\Rove\` | `~/.local/lib/rove/` |
| Its name on PATH | — | `~/.local/bin/rove`, a link to the program |
| How the desktop finds it | a Start Menu shortcut | `~/.local/share/applications/rove.desktop` |
| Icons | inside the program | `~/.local/share/icons/hicolor/…` |
| Notes to itself | `%LOCALAPPDATA%\rove\installed.json` | `~/.local/state/rove/installed.json` |

Windows also gets an entry in Installed Apps, so Rove uninstalls the way
everything else does. None of this needs an administrator: it is your install,
in your account.

The program and its libraries live together in one folder, which is why Linux
gets a link in `~/.local/bin` rather than the program itself. `~/.local/bin` is
on the PATH on most Linux systems; if yours is not, add it and `rove` works
from a terminal too.

## Updates

Download a newer build, run it once, and it replaces the installed one. The
newest build you have run is the one that stays installed — running an older
copy afterwards does not drag the install backwards, which is how every
self-updating program behaves. If you really do want to go back to an older
build, run it with `--install` and it will take over.

Rove also checks GitHub for a newer release on its own, once a day at most,
on a background thread that never makes the window wait. By default it only
tells you: a line in the status bar says a new version exists, and `Ctrl+U`
(or "Check for Updates Now" in the palette) downloads and installs it. Turn
on "Auto-update" in Settings and Rove does that download and install itself,
the moment it finds a newer release, and only tells you once it's done —
"restart Rove to use it."

Either way, installing a downloaded release runs it through the same
`--install` step described above, so it is exactly as if you had downloaded
and run the build yourself.

## Doing it by hand

| Command | What it does |
| --- | --- |
| `rove --install` | Installs the copy you ran, newer or not |
| `rove --uninstall` | Removes the program, the shortcut or desktop entry, the icons, and the notes |

`--uninstall` leaves your keybindings file alone, and leaves whatever copy you
ran the command from where it was.

Set `ROVE_NO_INSTALL=1` and Rove will not install itself at all — useful when
you want to run a build from a folder and leave the machine untouched.

## What is in a download

Rove is compiled to native code ahead of time, so there is no runtime to
install and nothing is compiled while it starts. That is why a build is a
folder: the program, plus SkiaSharp and HarfBuzz, which draw everything you
see and cannot be folded into it.

## What it costs at startup

Nothing you can measure. The check happens on a background thread, and all it
does is read one small file, see its own build described in it, and stop. A
launch with nothing to install writes nothing at all.

## If a package manager installed Rove

If a distribution package owns the desktop entry (there is a `rove.desktop` in
`/usr/share/applications`), Rove leaves the whole thing alone — the package
manager is in charge, and Rove does not fight it.
