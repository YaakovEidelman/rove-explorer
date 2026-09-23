# Keybindings

Rove ships with a full set of keys built in. You only need this file if you
want to change some of them.

## Where the file goes

| OS            | Path                                         |
| ------------- | -------------------------------------------- |
| Windows       | `%APPDATA%\rove\keybindings.json`             |
| Linux / macOS | `$XDG_CONFIG_HOME/rove/keybindings.json`, or `~/.config/rove/keybindings.json` |

If the file isn't there, Rove uses its defaults and says nothing. Rove reads
it once, at startup — restart the app after editing.

If something in the file can't be used (a key it doesn't recognise, a command
that doesn't exist), Rove keeps the rest and shows one line in the status bar
telling you what it skipped.

## What goes in it

One object per mode, mapping a key to a command:

```json
{
  // Comments and trailing commas are fine.
  "browse": {
    "ctrl+j": "content.move_down",
    "ctrl+k": "content.move_up",
    "d": null
  },
  "palette": {
    "ctrl+y": "palette.execute"
  }
}
```

- A key you list **replaces** whatever that key did by default.
- A key you don't list keeps its default. You only write down what you change.
- `null` (or `""`) **unbinds** the key, so it does nothing at all.

### Giving a command a new key moves it

When you point a key at a command, that command **moves there**: the keys it
came with stop working, in that mode. The example above puts "move down" on
`ctrl+j`, so plain `j` no longer moves down — which is the whole point of
moving it.

Only the command you named is affected. Everything you didn't mention keeps
its keys exactly as they were.

If you want a command on two keys at once, write both of them down:

```json
{
  "browse": {
    "ctrl+j": "content.move_down",
    "j": "content.move_down"
  }
}
```

### Modes

A mode is whatever surface is currently in front of you.

| Mode            | When it applies             |
| --------------- | --------------------------- |
| `browse`        | the file list                |
| `palette`       | the command palette overlay  |
| `localsearch`   | the filter box               |
| `globalsearch`  | the deep-search overlay      |
| `renameitem`    | the inline rename box        |
| `createitem`    | the new file/folder input    |
| `editpath`      | the path box at the top      |
| `pathcompletion` | the Tab completions under the path box |
| `confirm`       | the confirm bar              |
| `resizecolumns` | the column-width bar         |
| `bookmarks`     | the bookmark list            |
| `addbookmark`   | typing a path to bookmark    |

### Writing a key

Modifiers first, joined with `+`, then the key: `ctrl+shift+p`, `alt+enter`,
`j`, `/`, `0`, `f5`.

Modifiers are `ctrl`, `alt`, `shift`, and `meta` (also spelled `win`, `super`,
`cmd`). Named keys: `space`, `enter`, `esc`, `tab`, `backspace`, `delete`,
`up`, `down`, `left`, `right`, `home`, `end`, `pageup`, `pagedown`, `insert`,
and `f1`–`f12`. Punctuation is written as itself: `/`, `.`, `,`, `-`, `=`,
`[`, `]`, `;`, `'`, `` ` ``, `\`.

## Commands

### Moving around

| Command                  | What it does              |
| ------------------------ | ------------------------- |
| `content.move_up`        | Move up                   |
| `content.move_down`      | Move down                 |
| `content.move_top`       | Jump to top               |
| `content.move_bottom`    | Jump to bottom            |
| `content.get_item`       | Open the highlighted item |
| `content.go_up_dir`      | Go up one directory       |
| `content.left`           | List view: go up a directory. Icon view: move left |
| `content.right`          | List view: open the highlighted item. Icon view: move right |
| `content.edit_path`      | Edit the path at the top  |
| `content.edit_path_apply` | Go to the typed path     |
| `content.edit_path_cancel` | Leave the path alone    |
| `content.path_complete`  | Complete the typed path   |
| `content.path_complete_up` | Completions: move up    |
| `content.path_complete_down` | Completions: move down |
| `content.path_complete_dismiss` | Completions: close the list |
| `nav.drives`             | Go to drive…              |
| `content.open_with`      | Open the highlighted file with a chosen app |
| `content.open_terminal`  | Open a terminal in the current folder |
| `nav.trash`              | Go to the trash           |
| `bookmark.toggle`        | Bookmark / remove bookmark |
| `bookmark.show`          | Open the bookmark list    |
| `bookmark.move_up`       | Bookmarks: move up        |
| `bookmark.move_down`     | Bookmarks: move down      |
| `bookmark.execute`       | Bookmarks: go to selected |
| `bookmark.remove`        | Bookmarks: forget selected |
| `bookmark.add_apply`     | Bookmarks: add the typed path |
| `bookmark.add_cancel`    | Bookmarks: cancel adding a path |
| `bookmark.go:0` … `:8`   | Go straight to bookmark 1-9 |

`content.edit_path` opens the path at the top as a text box holding where
you are. It takes anything a shell would: an absolute path, one relative to
where you are, `~`, or an environment variable (`$HOME` on Linux, `%TEMP%` on
Windows). Type a file rather than a folder and Rove opens the folder around
it with that file highlighted.

`Tab` in that box completes what you have typed against what is really in the
folder, the way a shell does. One match is filled in outright; several carry
the text as far as they all agree and drop a list out below. While the list is
showing, `Ctrl+N` moves down it and `Ctrl+P` moves up. Each name you land on is
filled into the path box, so `Enter` always goes to what the box holds. `Tab`
keeps completing from what is in the box, and `Esc` closes the list without
leaving the path box. Completing a folder leaves the separator on the end, so
`Tab` again carries on inside it.

### Open Terminal Here

`content.open_terminal` ("Open Terminal Here" in the command palette) opens a terminal in the
folder you are looking at. On Linux it uses the program named in the `TERMINAL` environment
variable, then `xdg-terminal-exec`, `x-terminal-emulator`, and a list of common terminals
(ghostty, kitty, alacritty, foot, wezterm, gnome-terminal, konsole, xfce4-terminal, tilix,
xterm). On Windows it uses Windows Terminal, or `cmd` if that is not installed. It does not work
inside a zip or the trash.

### Open With

`content.open_with` ("Open With…" in the command palette) works on the highlighted file. On
Linux it lists only the apps the system says can open that kind of file, the default first. The
last entry, "Other apps…", lists every installed app that takes a file, for when the right one
is not registered for the type. Choosing an app opens the file with it. On Windows it hands off
to the system's own "Open With" dialog instead of building a list, since Windows already owns
that picker. It works on files only — with a folder highlighted, it shows greyed out in the
command palette and cannot be chosen.

When `Enter` opens a file and nothing on the system is set up to open it, Rove puts up the
same app list (or, on Windows, the same system dialog) instead of only saying so.

`Enter` on a compiled program with its execute bit set (an ELF binary, an AppImage) runs it,
the way Nautilus does, from the folder it sits in. Scripts are not run; they open in
whatever opens text.

### Finding things

| Command              | What it does           |
| -------------------- | ---------------------- |
| `content.search_local` | Filter this folder   |
| `search.global`      | Search from here (deep) |
| `search.move_up`     | Search: move up        |
| `search.move_down`   | Search: move down      |
| `search.execute`     | Search: open selected  |
| `palette.toggle`     | Command palette        |
| `palette.move_up`    | Palette: move up       |
| `palette.move_down`  | Palette: move down     |
| `palette.execute`    | Palette: run selected  |
| `quickaccess.next_tab` | Commands/Search/Bookmarks/Settings: next tab |
| `quickaccess.previous_tab` | Commands/Search/Bookmarks/Settings: previous tab |

The command palette, deep search, bookmark list, and settings share one card
on screen — whichever one you opened decides what's showing, and `Tab` /
`Shift+Tab` move to the next or previous of the four without closing the
card.

### The command palette

Every entry has a category (File, Navigation, View, Search, Bookmarks, Tabs,
App). With the search box empty, entries are shown under a heading for their
category, in a fixed order within it — not alphabetical — and the last few
commands you ran show first, under a "Recent" heading of their own.

Typing searches the title, the category, and a handful of keywords per
command (so "trash" finds "Delete", and "remove" finds "Put Back") — the
words can be typed in any order, so "new tab" and "tab new" both find
"New Tab". Matched letters in the title are highlighted.

A command that can't do anything right now — "Put Back" outside the trash,
"Extract Zip Here" without an archive selected — is left out of the list
rather than shown disabled.

### Acting on files

| Command                     | What it does                 |
| --------------------------- | ---------------------------- |
| `content.toggle_mark`       | Mark / unmark item           |
| `content.clear_marks`       | Clear all marks              |
| `content.toggle_hidden`     | Show / hide hidden items     |
| `content.copy`              | Copy items                   |
| `content.cut`               | Cut items                    |
| `content.paste`             | Paste items                  |
| `content.copy_path`         | Copy full path               |
| `content.extract`           | Extract a zip here           |
| `content.compress`          | Compress to a zip            |
| `content.delete`            | Delete (Recycle Bin / Trash) |
| `content.delete_permanent`  | Delete permanently           |
| `content.restore_trashed`   | Put back, out of the trash   |
| `content.restore_all_trashed` | Put everything back, out of the trash |
| `content.empty_trash`       | Empty the trash               |
| `content.rename`            | Rename item                  |
| `content.rename_apply`      | Apply the rename             |
| `content.create_file`       | New file                     |
| `content.create_folder`     | New folder                   |
| `content.create_apply`      | Apply the create             |
| `content.create_cancel`     | Cancel the create            |
| `content.cancel_operation`  | Cancel a running copy/move/delete |
| `content.undo`              | Undo the last action         |
| `confirm.accept`            | Confirm: yes (`y`)            |
| `confirm.cancel`            | Confirm: no (`n`/`Esc`)       |
| `confirm.select`            | Confirm: run the highlighted button (`Enter`) |
| `confirm.move_left`         | Confirm: highlight Cancel (`h`) |
| `confirm.move_right`        | Confirm: highlight Confirm (`l`) |

`content.toggle_hidden` (`.`) shows the items the system keeps out of the way
— dot-files on Linux, hidden-flagged ones on Windows. It stays on until you
turn it off, in every folder, and the status bar says so while it is. Files
the OS marks as its own are never listed either way.

`content.undo` walks back the last twenty actions, one press per action: a
rename goes back to the old name, a paste is taken back off the destination,
a cut-and-paste goes home, and a delete comes back out of the Recycle Bin (on
Linux, out of the trash). A permanent delete is the one thing it cannot undo —
that is what makes it permanent. Undoing a new file or folder only removes it
while it is still empty; once you have put something in it, it is yours.

`nav.trash` walks into the trash on Linux. The path bar shows it as "Trash"
rather than the real folder it lives at, and most verbs that write — rename,
cut, paste, new file/folder, compress, extract, and plain delete — are
refused there, same idea as inside a zip. Looking around, marking, and
copying still work, and so does putting things back or deleting them for
good, since that is what the trash is for. `content.restore_trashed` puts
the highlighted or marked items back where they were deleted from, reading
the record the trash keeps of each; `content.restore_all_trashed` does the
same for everything in the trash, regardless of how deep you have browsed
into it. `content.empty_trash` permanently deletes everything in the trash,
after confirming — the same one-way trip as `content.delete_permanent`. On
Windows the Recycle Bin is not a folder anything can list, so `nav.trash`
opens it in Explorer instead, and the other three commands do nothing there.

`bookmark.toggle` (`b`) keeps the highlighted item — or, in an empty folder,
the folder you are standing in — and pressing it again on the same thing
forgets it. There is no bookmark bar anywhere: the list only exists while
`bookmark.show` (`B`, or "Bookmarks…" in the palette) has it on screen, where
typing narrows it, Ctrl+N and Ctrl+P move through it, Enter goes, and Ctrl+D
forgets the one under the highlight.

The first nine bookmarks also answer to Ctrl+1 through Ctrl+9, from anywhere.
The key belongs to the position rather than to the bookmark, so a tenth
bookmark simply has no key until something ahead of it is forgotten. A
bookmarked folder opens; a bookmarked file puts you in the folder holding it,
with the file under the highlight.

The bookmark list always has an "Add a bookmark by path…" row pinned at the
top. `Enter` on it opens a text box — type any path (`~`, an environment
variable, or a plain absolute path all work) and `Enter` bookmarks it,
`Esc` cancels. This is the only way to bookmark a folder you cannot get
`bookmark.toggle` to land on directly, such as `/` once something inside it
is highlighted.

### Columns

| Command                        | What it does           |
| ------------------------------ | ---------------------- |
| `content.resize_columns`       | Open the width bar     |
| `content.column_next`          | Next column            |
| `content.column_prev`          | Previous column        |
| `content.column_grow`          | Wider                  |
| `content.column_shrink`        | Narrower               |
| `content.column_grow_large`    | Much wider             |
| `content.column_shrink_large`  | Much narrower          |
| `content.column_reset`         | Reset width            |

### View

| Command                     | What it does                          |
| ---------------------------- | -------------------------------------- |
| `content.toggle_view`        | Cycle: list → small icons → medium → large → list |
| `content.toggle_group_by_date` | Group by date when sorted by date modified |

`content.toggle_group_by_date` (palette-only, like the sort commands) splits the list view into
date headings — Today, Yesterday, Earlier This Week, Last Week, Earlier This Month, Last Month,
Earlier This Year, Last Year, then by calendar year — whenever the folder is sorted by date
modified. It does nothing while sorted by name, type, or size, and it does nothing in icon view.
It starts on, and the Downloads folder sorts by date modified by default, so a fresh Downloads
folder already shows the date headings; "Group by date when sorted by date modified" in Settings
changes the default for new tabs.

`content.toggle_view` (`i`) is the one key for all of it: press it again to
step to the next size, and once more past the largest to land back on the
list. In icon view, `hjkl` move through the grid like arrow keys instead of
opening or navigating — `j`/`k` skip a whole row and stop at the top or
bottom rather than wrapping, `h`/`l` step one item left or right. `Enter`
still opens the highlighted item and `Backspace` still goes up a directory,
in both views.

Column widths and sorting only apply to the list view. In icon view the width
bar won't open, the sort commands do nothing, and switching to icon view
closes the width bar if it was up.

### Locked folders (Linux)

When a folder says "Access denied", Rove shows the standard polkit password dialog and then
lists the folder as root through a helper started with `pkexec`. The helper stays up for the rest
of the session, so you type the password once; folders that need it open with no more questions.
The tab shows a red ADMIN mark next to its name while it is in this view.

The preview pane works here, and `Enter` on a file opens it. You can't read a root-only file
yourself, so the helper makes a read-only copy in a private folder (`$XDG_RUNTIME_DIR`, else the
temp folder) and the copy is what opens. Rove says so in the status bar, and changes to the copy
are not saved to the original. Copies are deleted when Rove closes; preview copies right away.

This view is read-only for now: delete, rename, new file/folder, cut, copy, paste, extract and
compress are refused. Going to a folder you can read normally leaves the view. It needs `pkexec`
and a running polkit agent, and only exists on Linux.

### Tabs

| Command          | What it does                     |
| ---------------- | -------------------------------- |
| `tab.new`        | New tab, on the same folder      |
| `tab.open_here`  | Open the highlighted folder in a new tab |
| `tab.close`      | Close the tab in front — the last one closes Rove |
| `tab.next`       | Next tab                         |
| `tab.previous`   | Previous tab                     |
| `tab.go:0` … `:8` | Go straight to tab 1-9          |

### App

| Command              | What it does            |
| -------------------- | ----------------------- |
| `app.toggle_preview` | Toggle the preview pane |
| `app.toggle_theme`   | Toggle light/dark       |
| `app.update_now`     | Check for updates now   |
| `app.close`          | Quit Rove               |

### Picker

`rove --picker` opens a stripped-down window — file list, breadcrumbs,
navigation, no tabs, no command palette — used by `rove-portal` as the Linux
file chooser backend. It uses the same `browse`-mode keymap as the main
window, so `content.get_item` and `content.escape` do double duty: `Enter` on
a file confirms the pick (on a folder it still navigates in), and `Esc`
cancels the picker outright rather than just clearing marks.

| Command                | What it does                                     |
| ---------------------- | ------------------------------------------------ |
| `picker.select_folder` | Choose the highlighted folder                    |
| `picker.cycle_filter`  | Switch to the next named file filter             |

`picker.select_folder` is only bound when the picker was asked for a folder.
It picks the marked folders if there are any (when several may be chosen),
otherwise the highlighted folder, otherwise the folder you're in. Default
key: `ctrl+o`.

`picker.cycle_filter` is only bound when the caller offered more than one
named filter (the portal's `filters` option) — it steps through them in
order, wrapping back to the first. Default key: `ctrl+f`.
