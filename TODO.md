# TODO

Rules for agents: this file is human-owned. Do NOT add, expand, reword, or "improve" any item
here. Only edit it when explicitly told to. As each item is finished, delete that item and
renumber the rest. When the last item is done, delete this entire file.

The following is in no particular order (other then that prod grade is first, but within a group, no particular order)

## Important for production grade app.
1. I need a full code review and check on the existing code front and backend. make sure everything is bullet proof and good. (This should be after everything)
2. allow apps own binary, to download, install, and update itself, by default, not auto, but with an option for auto.
3. on Linux (GNOME/Nautilus at least), double-clicking the extracted `Rove` binary fails with an error like "there is no application installed for application/x-pie-executable files." `xdg-mime query default application/x-pie-executable` returns nothing — GNOME has no default-app mechanism for a raw downloaded ELF binary the way it does for documents, only for scripts with a shebang. Works fine via right-click -> "Run as a Program", or from a terminal, just not a plain double-click. docs/installing.md's "unpack it and run it" pitch doesn't hold as-is on GNOME out of the box.

## Important but can still be published without it.
1. on Windows, going to trash just opens the Windows Recycle Bin instead of showing trashed items inside Rove. Doesn't need to be in the same commit as other work. Also, on both Windows and Linux, going to trash shouldn't make you lose your place - right now on Linux it navigates you into the trash folder and you lose where you just were. Needs something better than plain folder navigation for viewing trash - maybe a palette, maybe something else, not sure yet.
2. no single-instance enforcement - opening a second Rove instance can cause buggy behaviour, since each instance keeps its own copy of settings/bookmarks/etc. in memory and last one to save wins, silently dropping the other instance's changes. Not sure of the right fix - maybe only allow one instance open at a time.
3. claiming the default file manager now force-kills whatever currently holds org.freedesktop.FileManager1 (e.g. Nautilus) so the claim takes effect right away. Deliberate on whether I actually want that, or whether there should at least be an option to skip the kill and only take over via normal D-Bus activation (i.e. only when nothing is currently running).
4. we need a pallate activated context menu to give "open with", or "open dir in terminal" etc..
5. it seems like a lot of file managers auto extract .tar.gz files on click (or double click), maybe we should do that?

## Important visual design stuff - last thing to do before publishing
1. the rename textbox is a tad to short. the text is crunching into the top and bottom. I don't want it to get bigger then the actual row, but I need it to look good.
2. We need to work on the design, look n feel. (ultimate goal is to be able to follow a linux theme based on user input) (This is after all backend is good)
3. I'm thinking of having the prompts and warnings in a differnt way than they are now.
4. we need a better pallate design.
5. we need a better way of displaying the settings.
6. we need to allow custom color schema changes within the app.
7. we need a nice first install show around.

## v2
1. I want to even support, ssh, ftp (including sftp and ftps support) and mobile devices.
2. An easy feedback screen, so users can send feedback from inside the app.
3. full drag and drop support. very large and easy to get wrong, needs its own focused pass.
