# TODO

Rules for agents: this file is human-owned. Do NOT add, expand, reword, or "improve" any item
here. Only edit it when explicitly told to. As each item is finished, delete that item and
renumber the rest. When the last item is done, delete this entire file.

The following is in no particular order (other then that prod grade is first, but within a group, no particular order)

## Important for production grade app.
1. I need a full code review and check on the existing code front and backend. make sure everything is bullet proof and good. (This should be after everything)
2. We need to work on the design, look n feel. (ultimate goal is to be able to follow a linux theme based on user input) (This is after all backend is good)
3. allow apps own binary, to download, install, and update itself, by default, not auto, but with an option for auto.
4. on Linux, Rove doesn't implement the org.freedesktop.FileManager1 D-Bus interface, so "show in folder" requests from other apps (e.g. Chrome, for a file that still exists) go to whichever app already owns that interface (Nautilus) instead of Rove. Rove only gets picked up when the fallback of just opening the folder kicks in (e.g. the file was deleted). Production blocking.
5. on Linux (GNOME/Nautilus at least), double-clicking the extracted `Rove` binary fails with an error like "there is no application installed for application/x-pie-executable files." `xdg-mime query default application/x-pie-executable` returns nothing — GNOME has no default-app mechanism for a raw downloaded ELF binary the way it does for documents, only for scripts with a shebang. Works fine via right-click -> "Run as a Program", or from a terminal, just not a plain double-click. docs/installing.md's "unpack it and run it" pitch doesn't hold as-is on GNOME out of the box.

## Important but can still be published without it.
1. the rename textbox is a tad to short. the text is crunching into the top and bottom. I don't want it to get bigger then the actual row, but I need it to look good.
2. on Windows, going to trash just opens the Windows Recycle Bin instead of showing trashed items inside Rove. Doesn't need to be in the same commit as other work. Also, on both Windows and Linux, going to trash shouldn't make you lose your place - right now on Linux it navigates you into the trash folder and you lose where you just were. Needs something better than plain folder navigation for viewing trash - maybe a palette, maybe something else, not sure yet.
3. full drag and drop support. very large and easy to get wrong, needs its own focused pass.
4. no single-instance enforcement - opening a second Rove instance can cause buggy behaviour, since each instance keeps its own copy of settings/bookmarks/etc. in memory and last one to save wins, silently dropping the other instance's changes. Not sure of the right fix - maybe only allow one instance open at a time.

## v2
1. I want to even support, ssh, ftp (including sftp and ftps support) and mobile devices.
2. An easy feedback screen, so users can send feedback from inside the app.
