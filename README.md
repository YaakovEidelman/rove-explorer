# Rove

A keyboard-first file manager, built for [Omarchy](https://omarchy.org) first.
It runs everywhere else on Linux too, and on Windows. Native, no runtime to
install, and quick to start. On Omarchy, Rove picks up your system theme
automatically.

## Linux vs. Windows

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
[Releases page](https://github.com/YaakovEidelman/rove-explorer/releases).

On Linux:

```
tar -xzf rove-linux-x64.tar.gz
./rove/Rove
```

On Windows, unpack the `.zip` and run `Rove.exe`.

That is the whole procedure — no installer, nothing to run by hand. The
first run installs Rove for your user account and adds it to your app
launcher; after that you can delete what you downloaded. See
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

On Omarchy (and other Arch-based distros), the SDK's default runtime
identifier doesn't match any published runtime, so pass it explicitly:

```
dotnet publish src/Rove.UI/Rove.UI.csproj -c Release -r linux-x64 -p:RuntimeIdentifier=linux-x64
```

## License

GPLv3. See `LICENSE`.
