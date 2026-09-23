# TODO

Rules for agents: this file is human-owned. Do NOT add, expand, reword, or "improve" any item
here. Only edit it when explicitly told to. As each item is finished, delete that item and
renumber the rest. When the last item is done, delete this entire file.

The following is in no particular order (other then that prod grade is first, but within a group, no particular order)

## Important for production grade app.
1. I need a full code review and check on the existing code front and backend. make sure everything is bullet proof and good. (This should be after everything)
2. allow apps own binary, to download, install, and update itself, by default, not auto, but with an option for auto.
3. on Linux (GNOME/Nautilus at least), double-clicking the extracted `Rove` binary fails with an error like "there is no application installed for application/x-pie-executable files." `xdg-mime query default application/x-pie-executable` returns nothing — GNOME has no default-app mechanism for a raw downloaded ELF binary the way it does for documents, only for scripts with a shebang. Works fine via right-click -> "Run as a Program", or from a terminal, just not a plain double-click. docs/installing.md's "unpack it and run it" pitch doesn't hold as-is on GNOME out of the box.
4. if a folder on linux says "Access Denied" and is enterable via sudo, we need to allow that to work.
5. Redo Open With properly. It is Linux only and needs a real pass on behaviour and tests.
6. Hand-test the published AOT builds on real Wayland and Windows, not only the headless tests.
7. keybind config file has to be simpler
8. bookmark menu has to give more control

## Important but can still be published without it.
1. claiming the default file manager now force-kills whatever currently holds org.freedesktop.FileManager1 (e.g. Nautilus) so the claim takes effect right away. Deliberate on whether I actually want that, or whether there should at least be an option to skip the kill and only take over via normal D-Bus activation (i.e. only when nothing is currently running).
2. it seems like a lot of file managers auto extract .tar.gz files on click (or double click), maybe we should do that?
3. Add a "Move To/Copy To/etc.." option, so a user doesn't have to manually cut/copy, paste.
4. Add a right-click context menu, so the mouse reaches the same commands as the palette.
5. Add a properties view for the highlighted item: folder size, owner, dates, permissions.
6. Browse, extract and compress more archive types: tar, tar.gz, 7z and rar (only zip today).
7. Add select all, and range select (marks are one at a time today).
8. Add a "Duplicate" command that copies an item next to itself under a new name.
9. Show thumbnails for images in icon view (there is only the preview pane today).
10. Accessibility: name and describe controls (AutomationProperties) so screen readers work.

### Admin stuff
1. Copy out of an admin folder: copy things from an admin folder into a normal folder. Rove reads as root and writes as you.
2. Write actions in an admin folder: create, rename, paste in and delete, all done by the root helper. This is the riskiest step.
3. Admin polish: a clear way to leave admin mode, how long the password stays valid, and what delete means for root (no user trash).

### Command palette

## Windows - doable, pushed off for a later release
1. on Windows, going to trash just opens the Windows Recycle Bin instead of showing trashed items inside Rove. Doable - would need to read and list the $Recycle.Bin format instead of just restoring by path - but not needed for this release.
2. the file picker portal only exists on Linux, so other apps' "Open File" dialogs can't open as Rove on Windows the way they can on Linux. Doable via a Windows shell extension, but not needed for this release.
3. Rove can't register itself as the default file manager on Windows the way it does on Linux over D-Bus. Doable through the registry, but not needed for this release.

## Important visual design stuff - last thing to do before publishing
1. We need to work on the design, look n feel. (ultimate goal is to be able to follow a linux theme based on user input) (This is after all backend is good)
4. we need a better way of displaying the settings.
5. we need to allow custom color schema changes within the app.
6. we need a nice first install show around.

## v2
1. I want to even support, ssh, ftp (including sftp and ftps support) and mobile devices.
2. An easy feedback screen, so users can send feedback from inside the app.
3. full drag and drop support. very large and easy to get wrong, needs its own focused pass.
4. allow a user to preset the default page to open when opening rove.
5. add all the column options for a user to view.
