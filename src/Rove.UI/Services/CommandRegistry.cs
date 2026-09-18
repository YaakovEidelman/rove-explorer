using Avalonia.Input;
using Rove.Core.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Rove.UI.Services;

public enum CommandKind
{
    /// <summary>Plumbing (list navigation, apply/cancel) — hidden from the palette.</summary>
    System,

    /// <summary>A verb the user can invoke from the palette.</summary>
    User,
}

public readonly record struct KeyStroke(Key Key, KeyModifiers Modifiers)
{
    public string Display()
    {
        string key = Key switch
        {
            Key.OemQuestion => "/",
            Key.Space => "Space",
            Key.Enter => "Enter",
            Key.Escape => "Esc",
            Key.Back => "Backspace",
            Key.Tab => "Tab",
            Key.Left => "←",
            Key.Right => "→",
            Key.Up => "↑",
            Key.Down => "↓",
            >= Key.D0 and <= Key.D9 => ((int)(Key - Key.D0)).ToString(CultureInfo.InvariantCulture),
            _ => Key.ToString(),
        };
        List<string> parts = [];
        if (Modifiers.HasFlag(KeyModifiers.Control)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(KeyModifiers.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(KeyModifiers.Shift)) parts.Add("Shift");
        parts.Add(key);
        return string.Join("+", parts);
    }
}

public readonly record struct Command(CommandDef Def, Action Method);

public readonly record struct CommandDef(string Id, string Title, CommandKind CommandKind)
{
    // Palette
    public static readonly CommandDef TogglePalette = new("palette.toggle", "Command Palette", CommandKind.System);
    public static readonly CommandDef PaletteMoveUp = new("palette.move_up", "Palette: Move Up", CommandKind.System);
    public static readonly CommandDef PaletteMoveDown = new("palette.move_down", "Palette: Move Down", CommandKind.System);
    public static readonly CommandDef PaletteExecute = new("palette.execute", "Palette: Run Selected", CommandKind.System);
    public static readonly CommandDef QuickAccessNextTab =
        new("quickaccess.next_tab", "Quick Access: Next Tab (Commands/Search/Bookmarks)", CommandKind.System);

    // Navigation
    public static readonly CommandDef ContentMoveUp = new("content.move_up", "Move Up", CommandKind.System);
    public static readonly CommandDef ContentMoveDown = new("content.move_down", "Move Down", CommandKind.System);
    public static readonly CommandDef ContentMoveTop = new("content.move_top", "Jump to Top", CommandKind.System);
    public static readonly CommandDef ContentMoveBottom = new("content.move_bottom", "Jump to Bottom", CommandKind.System);
    public static readonly CommandDef ContentGetItem = new("content.get_item", "Open Selected Item", CommandKind.User);
    public static readonly CommandDef ContentGoUpDirectory = new("content.go_up_dir", "Go Up One Directory", CommandKind.User);
    public static readonly CommandDef ContentGoBack = new("content.go_back", "Back", CommandKind.User);
    public static readonly CommandDef ContentGoForward = new("content.go_forward", "Forward", CommandKind.User);
    public static readonly CommandDef ContentLeft =
        new("content.left", "Left (Up a Directory in List View, Move Left in Icon View)", CommandKind.User);
    public static readonly CommandDef ContentRight =
        new("content.right", "Right (Open in List View, Move Right in Icon View)", CommandKind.User);
    public static readonly CommandDef ToggleEditPath = new("content.edit_path", "Edit Path", CommandKind.User);
    public static readonly CommandDef ApplyEditPath = new("content.edit_path_apply", "Apply Path", CommandKind.System);
    public static readonly CommandDef CancelEditPath = new("content.edit_path_cancel", "Cancel Path Edit", CommandKind.System);
    public static readonly CommandDef CompletePath = new("content.path_complete", "Complete Path", CommandKind.System);
    public static readonly CommandDef PathCompleteMoveUp = new("content.path_complete_up", "Completions: Move Up", CommandKind.System);
    public static readonly CommandDef PathCompleteMoveDown = new("content.path_complete_down", "Completions: Move Down", CommandKind.System);
    public static readonly CommandDef PathCompleteAccept = new("content.path_complete_accept", "Completions: Take Selected", CommandKind.System);
    public static readonly CommandDef PathCompleteDismiss = new("content.path_complete_dismiss", "Completions: Close List", CommandKind.System);

    // Search
    public static readonly CommandDef ToggleLocalSearch = new("content.search_local", "Filter This Folder", CommandKind.User);
    public static readonly CommandDef ApplyLocalSearch = new("content.search_local_apply", "Filter: Show Results", CommandKind.System);
    public static readonly CommandDef ToggleGlobalSearch = new("search.global", "Search From Here (Deep)", CommandKind.User);
    public static readonly CommandDef GlobalSearchMoveUp = new("search.move_up", "Search: Move Up", CommandKind.System);
    public static readonly CommandDef GlobalSearchMoveDown = new("search.move_down", "Search: Move Down", CommandKind.System);
    public static readonly CommandDef GlobalSearchExecute = new("search.execute", "Search: Open Selected", CommandKind.System);

    // Item verbs
    public static readonly CommandDef ToggleRenameItem = new("content.rename", "Rename Item", CommandKind.User);
    public static readonly CommandDef ApplyRename = new("content.rename_apply", "Apply Rename", CommandKind.System);
    public static readonly CommandDef ToggleMarkItem = new("content.toggle_mark", "Mark/Unmark Item", CommandKind.User);
    public static readonly CommandDef ClearMarks = new("content.clear_marks", "Clear All Marks", CommandKind.User);
    public static readonly CommandDef EscapeBrowse = new("content.escape", "Clear Marks / Filter", CommandKind.System);
    public static readonly CommandDef ToggleShowHidden = new("content.toggle_hidden", "Show/Hide Hidden Files", CommandKind.User);
    public static readonly CommandDef DeleteItems =
        new("content.delete", $"Delete ({TrashService.DisplayName})", CommandKind.User);
    public static readonly CommandDef DeleteItemsPermanent = new("content.delete_permanent", "Delete Permanently…", CommandKind.User);
    public static readonly CommandDef RestoreTrashedItems =
        new("content.restore_trashed", $"Put Back (out of the {TrashService.DisplayName})", CommandKind.User);
    public static readonly CommandDef RestoreAllTrashedItems =
        new("content.restore_all_trashed", $"Put Everything Back (out of the {TrashService.DisplayName})", CommandKind.User);
    public static readonly CommandDef EmptyTrash =
        new("content.empty_trash", $"Empty the {TrashService.DisplayName}…", CommandKind.User);
    public static readonly CommandDef CopyItems = new("content.copy", "Copy Items", CommandKind.User);
    public static readonly CommandDef CutItems = new("content.cut", "Cut Items", CommandKind.User);
    public static readonly CommandDef PasteItems = new("content.paste", "Paste Items", CommandKind.User);
    public static readonly CommandDef CopyPath = new("content.copy_path", "Copy Full Path", CommandKind.User);
    public static readonly CommandDef ExtractArchives = new("content.extract", "Extract Zip Here", CommandKind.User);
    public static readonly CommandDef CompressItems = new("content.compress", "Compress to Zip", CommandKind.User);
    public static readonly CommandDef ToggleCreateFile = new("content.create_file", "New File", CommandKind.User);
    public static readonly CommandDef ToggleCreateFolder = new("content.create_folder", "New Folder", CommandKind.User);
    public static readonly CommandDef ApplyCreate = new("content.create_apply", "Apply Create", CommandKind.System);
    public static readonly CommandDef CancelCreate = new("content.create_cancel", "Cancel Create", CommandKind.System);

    // Undo
    public static readonly CommandDef UndoLastAction = new("content.undo", "Undo Last Action", CommandKind.User);

    // Columns
    public static readonly CommandDef ToggleResizeColumns = new("content.resize_columns", "Resize Columns", CommandKind.User);
    public static readonly CommandDef ColumnNext = new("content.column_next", "Columns: Next Column", CommandKind.System);
    public static readonly CommandDef ColumnPrev = new("content.column_prev", "Columns: Previous Column", CommandKind.System);
    public static readonly CommandDef ColumnGrow = new("content.column_grow", "Columns: Wider", CommandKind.System);
    public static readonly CommandDef ColumnShrink = new("content.column_shrink", "Columns: Narrower", CommandKind.System);
    public static readonly CommandDef ColumnGrowLarge = new("content.column_grow_large", "Columns: Much Wider", CommandKind.System);
    public static readonly CommandDef ColumnShrinkLarge = new("content.column_shrink_large", "Columns: Much Narrower", CommandKind.System);
    public static readonly CommandDef ColumnResetWidth = new("content.column_reset", "Columns: Reset Width", CommandKind.System);
    public static readonly CommandDef SortByActiveColumn = new("content.sort_active_column", "Columns: Sort by This Column", CommandKind.System);

    // Sort
    public static readonly CommandDef SortByName = new("content.sort_name", "Sort by Name", CommandKind.User);
    public static readonly CommandDef SortByType = new("content.sort_type", "Sort by Type", CommandKind.User);
    public static readonly CommandDef SortBySize = new("content.sort_size", "Sort by Size", CommandKind.User);
    public static readonly CommandDef SortByModified = new("content.sort_modified", "Sort by Date Modified", CommandKind.User);

    // View
    public static readonly CommandDef ToggleContentView = new("content.toggle_view", "Cycle List/Icon View", CommandKind.User);

    // Drives — one command per mounted drive, registered as they come and go.
    public const string DriveIdPrefix = "nav.drive:";
    public static readonly CommandDef ShowDrives = new("nav.drives", "Go to Drive…", CommandKind.User);
    public static readonly CommandDef ShowTrash = new("nav.trash", $"Go to the {TrashService.DisplayName}", CommandKind.User);

    // Bookmarks — the list itself is never on screen except as this surface.
    public const string BookmarkGoIdPrefix = "bookmark.go:";
    public static readonly CommandDef ToggleBookmark = new("bookmark.toggle", "Bookmark / Remove Bookmark", CommandKind.User);
    public static readonly CommandDef ShowBookmarks = new("bookmark.show", "Bookmarks…", CommandKind.User);
    public static readonly CommandDef BookmarkMoveUp = new("bookmark.move_up", "Bookmarks: Move Up", CommandKind.System);
    public static readonly CommandDef BookmarkMoveDown = new("bookmark.move_down", "Bookmarks: Move Down", CommandKind.System);
    public static readonly CommandDef BookmarkExecute = new("bookmark.execute", "Bookmarks: Go to Selected", CommandKind.System);
    public static readonly CommandDef RemoveBookmark = new("bookmark.remove", "Bookmarks: Remove Selected", CommandKind.System);

    /// <summary>The command that goes to the nth bookmark, counting from zero.</summary>
    public static CommandDef BookmarkGo(int index) =>
        new($"{BookmarkGoIdPrefix}{index}", $"Go to Bookmark {index + 1}", CommandKind.System);

    // Confirm surface
    public static readonly CommandDef ConfirmAccept = new("confirm.accept", "Confirm: Yes", CommandKind.System);
    public static readonly CommandDef ConfirmCancel = new("confirm.cancel", "Confirm: No", CommandKind.System);

    // Long-running file operations
    public static readonly CommandDef CancelFileOperation = new("content.cancel_operation", "Cancel Running File Operation", CommandKind.User);

    // Tabs — one folder view each, with the keymap pointed at whichever is in front.
    public const string TabGoIdPrefix = "tab.go:";

    /// <summary>How many tabs a number key can reach.</summary>
    public const int TabShortcutCount = 9;
    public static readonly CommandDef NewTab = new("tab.new", "New Tab", CommandKind.User);
    public static readonly CommandDef OpenInNewTab = new("tab.open_here", "Open in a New Tab", CommandKind.User);
    public static readonly CommandDef CloseTab = new("tab.close", "Close Tab", CommandKind.User);
    public static readonly CommandDef NextTab = new("tab.next", "Next Tab", CommandKind.User);
    public static readonly CommandDef PreviousTab = new("tab.previous", "Previous Tab", CommandKind.User);
    public static readonly CommandDef ToggleRenameTab = new("tab.rename", "Rename Tab", CommandKind.User);
    public static readonly CommandDef ApplyRenameTab = new("tab.rename_apply", "Apply Tab Rename", CommandKind.System);

    /// <summary>The command that goes to the nth tab, counting from zero.</summary>
    public static CommandDef TabGo(int index) =>
        new($"{TabGoIdPrefix}{index}", $"Go to Tab {index + 1}", CommandKind.System);

    // App
    public static readonly CommandDef TogglePreview = new("app.toggle_preview", "Toggle Preview Pane", CommandKind.User);
    public static readonly CommandDef ToggleTheme = new("app.toggle_theme", "Toggle Light/Dark Theme", CommandKind.User);
    public static readonly CommandDef ToggleMaximize = new("app.toggle_maximize", "Toggle Maximize/Restore Window", CommandKind.User);
    public static readonly CommandDef MinimizeWindow = new("app.minimize", "Minimize Window", CommandKind.User);
    public static readonly CommandDef CloseApp = new("app.close", "Quit Rove", CommandKind.User);
    public static readonly CommandDef CheckForUpdates = new("app.update_now", "Check for Updates Now", CommandKind.User);

    // Picker (the --picker launch mode)
    public static readonly CommandDef PickerSelectCurrentFolder =
        new("picker.select_current_folder", "Choose the Current Folder", CommandKind.System);
    public static readonly CommandDef PickerCycleFilter =
        new("picker.cycle_filter", "Switch File Type Filter", CommandKind.System);

    // Settings
    public static readonly CommandDef ShowSettings = new("app.settings", "Settings…", CommandKind.User);
    public static readonly CommandDef SettingsMoveUp = new("settings.move_up", "Settings: Move Up", CommandKind.System);
    public static readonly CommandDef SettingsMoveDown = new("settings.move_down", "Settings: Move Down", CommandKind.System);
    public static readonly CommandDef SettingsActivate = new("settings.activate", "Settings: Change Selected", CommandKind.System);
}

public class CommandRegistry : ICommandTarget
{
    private readonly Dictionary<Mode, Dictionary<KeyStroke, string>> _modeBindings = [];
    private readonly Dictionary<string, Command> _commands = [];

    public CommandRegistry() : this(KeymapLoad.Empty)
    {
    }

    /// <summary>
    /// Built-in defaults first, then whatever the user's keybindings file
    /// says on top — so a config only has to mention what it changes.
    ///
    /// <para>
    /// Giving a command a key of your own <em>moves</em> it: the keys that
    /// command had built in stop working, so binding "ctrl+j" to move-down
    /// does not leave plain "j" doing it as well. Keys belonging to commands
    /// the file never mentions are untouched.
    /// </para>
    /// </summary>
    public CommandRegistry(KeymapLoad keymap)
    {
        foreach (Mode mode in Enum.GetValues<Mode>())
            _modeBindings.Add(mode, []);

        HashSet<(Mode Mode, string Action)> remapped =
            [.. keymap.Overrides
                .Where(o => o.Action is { Length: > 0 })
                .Select(o => (o.Mode, o.Action!))];

        foreach (KeyValuePair<Mode, KommandShortcut[]> map in KeymapDefaults.DefaultModeBindings)
        {
            foreach (KommandShortcut ks in map.Value)
            {
                if (!remapped.Contains((map.Key, ks.Action)))
                    Bind(map.Key, ks);
            }
        }

        foreach (KeymapOverride entry in keymap.Overrides)
            Rebind(entry);
    }

    public void Bind(Mode mode, KommandShortcut ks) => _modeBindings[mode].TryAdd(ks.Stroke, ks.Action);

    /// <summary>Replaces (or, with a null action, removes) one binding.</summary>
    public void Rebind(KeymapOverride entry)
    {
        if (entry.Action is { Length: > 0 } action)
            _modeBindings[entry.Mode][entry.Stroke] = action;
        else
            _modeBindings[entry.Mode].Remove(entry.Stroke);
    }

    public void Register(CommandDef def, Action method) => _commands[def.Id] = new(def, method);

    /// <summary>Drops a command again — for entries that come and go, like one per drive.</summary>
    public void Unregister(string commandId) => _commands.Remove(commandId);

    public IEnumerable<string> CommandIdsStartingWith(string prefix) =>
        [.. _commands.Keys.Where(id => id.StartsWith(prefix, StringComparison.Ordinal))];

    public bool TryExecute(Mode mode, KeyStroke stroke)
    {
        if (!_modeBindings.TryGetValue(mode, out Dictionary<KeyStroke, string>? binding)) return false;
        if (!binding.TryGetValue(stroke, out string? action)) return false;
        return TryExecute(action);
    }

    public bool TryExecute(string action)
    {
        if (!_commands.TryGetValue(action, out Command command)) return false;
        command.Method();
        return true;
    }

    /// <summary>Keyboard hint shown next to a palette entry (Browse binding wins).</summary>
    public string HintFor(string commandId)
    {
        foreach (Mode mode in Enum.GetValues<Mode>())
        {
            foreach (KeyValuePair<KeyStroke, string> pair in _modeBindings[mode])
            {
                if (pair.Value == commandId)
                    return pair.Key.Display();
            }
        }
        return "";
    }

    public Command[] Commands() =>
        [.. _commands.Values
            .Where(c => c.Def.CommandKind == CommandKind.User)
            .OrderBy(c => c.Def.Title, StringComparer.OrdinalIgnoreCase)];

    public Command[] FilteredCommands(string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
            return Commands();

        return [.. _commands.Values
            .Where(c => c.Def.CommandKind == CommandKind.User)
            .Select(c => (Command: c, Matched: FuzzyMatcher.TryMatch(filter, c.Def.Title, out int score), Score: score))
            .Where(t => t.Matched)
            .OrderByDescending(t => t.Score)
            .ThenBy(t => t.Command.Def.Title, StringComparer.OrdinalIgnoreCase)
            .Select(t => t.Command)];
    }
}
