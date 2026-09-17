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
