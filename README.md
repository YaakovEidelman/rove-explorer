# Rove

A fast, keyboard-first file manager for Windows and Linux. Native, no
runtime to install, and quick to start.

## Features

- A command palette for everything — search for an action by name instead of
  hunting through menus.
- Tabs, bookmarks, and a global search that looks across more than one
  folder at a time.
- Zip archives: browse into one without extracting it, or unpack and pack
  from the file list.
- Image and text preview, plus a look at a file's Unix permissions, right in
  the preview pane.
- A configurable keymap — see `docs/keybindings.md`.

## Windows vs. Linux

Rove runs fully on both. A few things are possible on Windows too, but are
beyond scope for now:

- **Browsing the Recycle Bin inside Rove.** Right now, opening it just
  hands off to Explorer instead of showing trashed items in Rove itself.
- **The file picker portal**, which lets other apps' "Open File" dialogs
  open as Rove. It exists on Linux; a Windows version hasn't been built.
- **Registering Rove as the default file manager**, the way it can on
  Linux. Doable on Windows too, just not implemented yet.

Everyday browsing, tabs, search, archives, and previews all work the same
on both.

## Installing

Grab a build from the
[Releases page](https://github.com/YaakovEidelman/rove-explorer/releases),
unpack it, and run it. No installer, nothing to run by hand — see
`docs/installing.md` for the details, including how updates work.

Tagged releases (`vX.Y.Z`) are the stable channel; a rolling `dev`
prerelease tracks the latest `main` for anyone who wants it. Rove checks for
a newer stable release on its own and tells you in the status bar — see
`docs/installing.md`.

## Building from source

Requires the .NET 10 SDK.

```
dotnet publish src/Rove.UI/Rove.UI.csproj -c Release -r <win-x64|linux-x64>
```

## License

GPLv3. See `LICENSE`.
