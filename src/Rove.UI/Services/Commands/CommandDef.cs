using Rove.Core.Services;

namespace Rove.UI.Services;

public readonly record struct CommandDef(
    string Id,
    string Title,
    CommandKind CommandKind,
    int Order = 0,
    CommandCategory Category = CommandCategory.None,
    string[]? Keywords = null)
{
    public static readonly CommandDef TogglePalette = new("palette.toggle", "Command Palette", CommandKind.System);
    public static readonly CommandDef PaletteMoveUp = new("palette.move_up", "Palette: Move Up", CommandKind.System);
    public static readonly CommandDef PaletteMoveDown = new("palette.move_down", "Palette: Move Down", CommandKind.System);
    public static readonly CommandDef PaletteExecute = new("palette.execute", "Palette: Run Selected", CommandKind.System);
    public static readonly CommandDef QuickAccessNextTab =
        new("quickaccess.next_tab", "Quick Access: Next Tab (Commands/Search/Bookmarks)", CommandKind.System);
    public static readonly CommandDef QuickAccessPreviousTab =
        new("quickaccess.previous_tab", "Quick Access: Previous Tab (Commands/Search/Bookmarks)", CommandKind.System);

    public static readonly CommandDef ContentMoveUp = new("content.move_up", "Move Up", CommandKind.System);
    public static readonly CommandDef ContentMoveDown = new("content.move_down", "Move Down", CommandKind.System);
    public static readonly CommandDef ContentMoveTop = new("content.move_top", "Jump to Top", CommandKind.System);
    public static readonly CommandDef ContentMoveBottom = new("content.move_bottom", "Jump to Bottom", CommandKind.System);
    public static readonly CommandDef ContentGetItem =
        new("content.get_item", "Open Selected Item", CommandKind.User, 0, CommandCategory.Navigation, ["enter", "launch"]);
    public static readonly CommandDef ContentGoUpDirectory =
        new("content.go_up_dir", "Go Up One Directory", CommandKind.User, 1, CommandCategory.Navigation, ["parent", "up"]);
    public static readonly CommandDef ContentGoBack =
        new("content.go_back", "Go Back", CommandKind.User, 2, CommandCategory.Navigation, ["previous", "history", "back"]);
    public static readonly CommandDef ContentGoForward =
        new("content.go_forward", "Go Forward", CommandKind.User, 3, CommandCategory.Navigation, ["next", "history", "forward"]);
    public static readonly CommandDef ContentLeft =
        new("content.left", "Left", CommandKind.User, 4, CommandCategory.Navigation,
            ["up a directory", "list view", "icon view", "move left"]);
    public static readonly CommandDef ContentRight =
        new("content.right", "Right", CommandKind.User, 5, CommandCategory.Navigation,
            ["open item", "list view", "icon view", "move right"]);
    public static readonly CommandDef ToggleEditPath =
        new("content.edit_path", "Edit Path", CommandKind.User, 6, CommandCategory.Navigation,
            ["path bar", "type path", "go to path"]);
    public static readonly CommandDef ApplyEditPath = new("content.edit_path_apply", "Apply Path", CommandKind.System);
    public static readonly CommandDef CancelEditPath = new("content.edit_path_cancel", "Cancel Path Edit", CommandKind.System);
    public static readonly CommandDef CompletePath = new("content.path_complete", "Complete Path", CommandKind.System);
    public static readonly CommandDef PathCompleteMoveUp = new("content.path_complete_up", "Completions: Move Up", CommandKind.System);
    public static readonly CommandDef PathCompleteMoveDown = new("content.path_complete_down", "Completions: Move Down", CommandKind.System);
    public static readonly CommandDef PathCompleteDismiss = new("content.path_complete_dismiss", "Completions: Close List", CommandKind.System);

    public static readonly CommandDef ToggleLocalSearch =
        new("content.search_local", "Filter This Folder", CommandKind.User, 0, CommandCategory.Search, ["find", "search", "filter"]);
    public static readonly CommandDef ApplyLocalSearch = new("content.search_local_apply", "Filter: Show Results", CommandKind.System);
    public static readonly CommandDef ToggleGlobalSearch =
        new("search.global", "Search From Here", CommandKind.User, 1, CommandCategory.Search,
            ["find", "recursive", "deep search"]);
    public static readonly CommandDef GlobalSearchMoveUp = new("search.move_up", "Search: Move Up", CommandKind.System);
    public static readonly CommandDef GlobalSearchMoveDown = new("search.move_down", "Search: Move Down", CommandKind.System);
    public static readonly CommandDef GlobalSearchExecute = new("search.execute", "Search: Open Selected", CommandKind.System);

    public static readonly CommandDef ToggleRenameItem =
        new("content.rename", "Rename Item", CommandKind.User, 0, CommandCategory.File, ["rename"]);
    public static readonly CommandDef ApplyRename = new("content.rename_apply", "Apply Rename", CommandKind.System);
    public static readonly CommandDef ToggleMarkItem =
        new("content.toggle_mark", "Toggle Mark", CommandKind.User, 1, CommandCategory.File, ["select", "unmark"]);
    public static readonly CommandDef ClearMarks =
        new("content.clear_marks", "Clear Marks", CommandKind.User, 2, CommandCategory.File, ["deselect", "unmark all"]);
    public static readonly CommandDef EscapeBrowse = new("content.escape", "Clear Marks / Filter", CommandKind.System);
    public static readonly CommandDef ToggleShowHidden =
        new("content.toggle_hidden", "Toggle Hidden Files", CommandKind.User, 3, CommandCategory.File,
            ["show hidden", "hide hidden", "dotfiles", "unhide"]);
    public static readonly CommandDef DeleteItems =
        new("content.delete", $"Delete ({TrashService.DisplayName})", CommandKind.User, 4, CommandCategory.File,
            ["remove", "trash", "recycle bin"]);
    public static readonly CommandDef DeleteItemsPermanent =
        new("content.delete_permanent", "Delete Permanently…", CommandKind.User, 5, CommandCategory.File,
            ["remove", "erase", "permanent"]);
    public static readonly CommandDef RestoreTrashedItems =
        new("content.restore_trashed", "Put Back", CommandKind.User, 6, CommandCategory.File,
            ["restore", "undelete", "untrash", TrashService.DisplayName]);
    public static readonly CommandDef RestoreAllTrashedItems =
        new("content.restore_all_trashed", "Put Everything Back", CommandKind.User, 7, CommandCategory.File,
            ["restore all", "undelete all", TrashService.DisplayName]);
    public static readonly CommandDef EmptyTrash =
        new("content.empty_trash", $"Empty the {TrashService.DisplayName}…", CommandKind.User, 8, CommandCategory.File,
            ["clear trash", "delete all"]);
    public static readonly CommandDef CopyItems =
        new("content.copy", "Copy Items", CommandKind.User, 9, CommandCategory.File, ["duplicate", "clipboard"]);
    public static readonly CommandDef CutItems =
        new("content.cut", "Cut Items", CommandKind.User, 10, CommandCategory.File, ["move", "clipboard"]);
    public static readonly CommandDef PasteItems =
        new("content.paste", "Paste Items", CommandKind.User, 11, CommandCategory.File, ["clipboard"]);
    public static readonly CommandDef CopyPath =
        new("content.copy_path", "Copy Full Path", CommandKind.User, 12, CommandCategory.File, ["clipboard", "location"]);
    public static readonly CommandDef ExtractArchives =
        new("content.extract", "Extract Zip Here", CommandKind.User, 13, CommandCategory.File,
            ["unzip", "decompress", "archive"]);
    public static readonly CommandDef CompressItems =
        new("content.compress", "Compress to Zip", CommandKind.User, 14, CommandCategory.File, ["zip", "archive"]);
    public static readonly CommandDef ToggleCreateFile =
        new("content.create_file", "New File", CommandKind.User, 15, CommandCategory.File, ["create", "add"]);
    public static readonly CommandDef ToggleCreateFolder =
        new("content.create_folder", "New Folder", CommandKind.User, 16, CommandCategory.File,
            ["create", "add", "directory"]);
    public static readonly CommandDef ApplyCreate = new("content.create_apply", "Apply Create", CommandKind.System);
    public static readonly CommandDef CancelCreate = new("content.create_cancel", "Cancel Create", CommandKind.System);

    public static readonly CommandDef UndoLastAction =
        new("content.undo", "Undo Last Action", CommandKind.User, 17, CommandCategory.File, ["revert", "undo"]);

    public static readonly CommandDef ToggleResizeColumns =
        new("content.resize_columns", "Resize Columns", CommandKind.User, 0, CommandCategory.View,
            ["column width", "widen", "narrow"]);
    public static readonly CommandDef ColumnNext = new("content.column_next", "Columns: Next Column", CommandKind.System);
    public static readonly CommandDef ColumnPrev = new("content.column_prev", "Columns: Previous Column", CommandKind.System);
    public static readonly CommandDef ColumnGrow = new("content.column_grow", "Columns: Wider", CommandKind.System);
    public static readonly CommandDef ColumnShrink = new("content.column_shrink", "Columns: Narrower", CommandKind.System);
    public static readonly CommandDef ColumnGrowLarge = new("content.column_grow_large", "Columns: Much Wider", CommandKind.System);
    public static readonly CommandDef ColumnShrinkLarge = new("content.column_shrink_large", "Columns: Much Narrower", CommandKind.System);
    public static readonly CommandDef ColumnResetWidth = new("content.column_reset", "Columns: Reset Width", CommandKind.System);
    public static readonly CommandDef SortByActiveColumn = new("content.sort_active_column", "Columns: Sort by This Column", CommandKind.System);

    public static readonly CommandDef SortByName =
        new("content.sort_name", "Sort by Name", CommandKind.User, 1, CommandCategory.View, ["order", "alphabetical"]);
    public static readonly CommandDef SortByType =
        new("content.sort_type", "Sort by Type", CommandKind.User, 2, CommandCategory.View, ["order", "extension", "kind"]);
    public static readonly CommandDef SortBySize =
        new("content.sort_size", "Sort by Size", CommandKind.User, 3, CommandCategory.View,
            ["order", "largest", "smallest"]);
    public static readonly CommandDef SortByModified =
        new("content.sort_modified", "Sort by Date Modified", CommandKind.User, 4, CommandCategory.View,
            ["order", "newest", "oldest", "time"]);

    public static readonly CommandDef ToggleContentView =
        new("content.toggle_view", "Cycle View", CommandKind.User, 5, CommandCategory.View,
            ["icons", "thumbnails", "grid", "list view"]);
    public static readonly CommandDef ToggleGroupByDate =
        new("content.toggle_group_by_date", "Group by Date", CommandKind.User, 6, CommandCategory.View,
            ["group", "cluster", "date headers", "explorer", "today", "yesterday"]);

    public const string DriveIdPrefix = "nav.drive:";
    public const string OpenWithIdPrefix = "open.with:";
    public const string OpenWithOthersId = OpenWithIdPrefix + "other-apps";
    public static readonly CommandDef OpenWith =
        new("content.open_with", "Open With…", CommandKind.User, 8, CommandCategory.Navigation,
            ["application", "app", "program"]);

    public static readonly CommandDef OpenTerminal =
        new("content.open_terminal", "Open Terminal Here", CommandKind.User, 9, CommandCategory.Navigation,
            ["console", "shell", "cmd"]);

    public static bool IsTransient(string id) =>
        id.StartsWith(OpenWithIdPrefix, StringComparison.Ordinal);

    public static readonly CommandDef ShowDrives =
        new("nav.drives", "Go to Drive…", CommandKind.User, 7, CommandCategory.Navigation,
            ["drives", "volumes", "disks"]);
    public static readonly CommandDef ShowTrash =
        new("nav.trash", $"Go to the {TrashService.DisplayName}", CommandKind.User, 10, CommandCategory.Navigation,
            ["recycle bin", "deleted items"]);

    public const string BookmarkGoIdPrefix = "bookmark.go:";
    public static readonly CommandDef ToggleBookmark =
        new("bookmark.toggle", "Toggle Bookmark", CommandKind.User, 0, CommandCategory.Bookmarks,
            ["favorite", "star", "pin", "remove bookmark"]);
    public static readonly CommandDef ShowBookmarks =
        new("bookmark.show", "Bookmarks…", CommandKind.User, 1, CommandCategory.Bookmarks,
            ["favorites", "pinned", "list"]);
    public static readonly CommandDef BookmarkMoveUp = new("bookmark.move_up", "Bookmarks: Move Up", CommandKind.System);
    public static readonly CommandDef BookmarkMoveDown = new("bookmark.move_down", "Bookmarks: Move Down", CommandKind.System);
    public static readonly CommandDef BookmarkExecute = new("bookmark.execute", "Bookmarks: Go to Selected", CommandKind.System);
    public static readonly CommandDef RemoveBookmark = new("bookmark.remove", "Bookmarks: Remove Selected", CommandKind.System);
    public static readonly CommandDef ApplyAddBookmark = new("bookmark.add_apply", "Bookmarks: Add Path", CommandKind.System);
    public static readonly CommandDef CancelAddBookmark = new("bookmark.add_cancel", "Bookmarks: Cancel Add", CommandKind.System);
    public static readonly CommandDef BookmarkCompletePath = new("bookmark.add_path_complete", "Bookmarks: Complete Path", CommandKind.System);
    public static readonly CommandDef BookmarkPathCompleteMoveUp =
        new("bookmark.add_path_complete_up", "Bookmarks: Completions Move Up", CommandKind.System);
    public static readonly CommandDef BookmarkPathCompleteMoveDown =
        new("bookmark.add_path_complete_down", "Bookmarks: Completions Move Down", CommandKind.System);
    public static readonly CommandDef BookmarkPathCompleteDismiss =
        new("bookmark.add_path_complete_dismiss", "Bookmarks: Close Completions", CommandKind.System);
    public static readonly CommandDef BookmarkMoveEntryUp =
        new("bookmark.reorder_up", "Bookmarks: Move Selected Up", CommandKind.System);
    public static readonly CommandDef BookmarkMoveEntryDown =
        new("bookmark.reorder_down", "Bookmarks: Move Selected Down", CommandKind.System);

    public static CommandDef BookmarkGo(int index) =>
        new($"{BookmarkGoIdPrefix}{index}", $"Go to Bookmark {index + 1}", CommandKind.System);

    public static readonly CommandDef ConfirmAccept = new("confirm.accept", "Confirm: Yes", CommandKind.System);
    public static readonly CommandDef ConfirmCancel = new("confirm.cancel", "Confirm: No", CommandKind.System);
    public static readonly CommandDef ConfirmSelect = new("confirm.select", "Confirm: Run Highlighted Option", CommandKind.System);
    public static readonly CommandDef ConfirmMoveLeft = new("confirm.move_left", "Confirm: Highlight Cancel", CommandKind.System);
    public static readonly CommandDef ConfirmMoveRight = new("confirm.move_right", "Confirm: Highlight Confirm", CommandKind.System);

    public static readonly CommandDef CancelFileOperation =
        new("content.cancel_operation", "Cancel Running File Operation", CommandKind.User, 18, CommandCategory.File,
            ["stop", "abort"]);

    public const string TabGoIdPrefix = "tab.go:";

    public const int TabShortcutCount = 9;
    public static readonly CommandDef NewTab =
        new("tab.new", "New Tab", CommandKind.User, 0, CommandCategory.Tabs, ["open tab", "create tab"]);
    public static readonly CommandDef OpenInNewTab =
        new("tab.open_here", "Open in New Tab", CommandKind.User, 1, CommandCategory.Tabs, ["tab", "folder"]);
    public static readonly CommandDef CloseTab =
        new("tab.close", "Close Tab", CommandKind.User, 2, CommandCategory.Tabs, ["tab"]);
    public static readonly CommandDef NextTab =
        new("tab.next", "Next Tab", CommandKind.User, 3, CommandCategory.Tabs, ["tab", "switch"]);
    public static readonly CommandDef PreviousTab =
        new("tab.previous", "Previous Tab", CommandKind.User, 4, CommandCategory.Tabs, ["tab", "switch", "back"]);
    public static readonly CommandDef ToggleRenameTab =
        new("tab.rename", "Rename Tab", CommandKind.User, 5, CommandCategory.Tabs, ["tab", "title"]);
    public static readonly CommandDef ApplyRenameTab = new("tab.rename_apply", "Apply Tab Rename", CommandKind.System);

    public static CommandDef TabGo(int index) =>
        new($"{TabGoIdPrefix}{index}", $"Go to Tab {index + 1}", CommandKind.System);

    public static readonly CommandDef TogglePreview =
        new("app.toggle_preview", "Toggle Preview Pane", CommandKind.User, 0, CommandCategory.App,
            ["preview", "sidebar", "pane"]);
    public static readonly CommandDef ToggleTheme =
        new("app.toggle_theme", "Toggle Theme", CommandKind.User, 1, CommandCategory.App,
            ["dark mode", "light mode", "appearance"]);
    public static readonly CommandDef ToggleMaximize =
        new("app.toggle_maximize", "Toggle Maximize", CommandKind.User, 2, CommandCategory.App,
            ["window", "fullscreen", "restore"]);
    public static readonly CommandDef MinimizeWindow =
        new("app.minimize", "Minimize Window", CommandKind.User, 3, CommandCategory.App, ["window", "taskbar"]);
    public static readonly CommandDef CloseApp =
        new("app.close", "Quit Rove", CommandKind.User, 4, CommandCategory.App, ["exit", "close app"]);
    public static readonly CommandDef CheckForUpdates =
        new("app.update_now", "Check for Updates Now", CommandKind.User, 5, CommandCategory.App,
            ["update", "upgrade", "version"]);

    public static readonly CommandDef PickerSelectFolder =
        new("picker.select_folder", "Choose the Folder", CommandKind.System);
    public static readonly CommandDef PickerCycleFilter =
        new("picker.cycle_filter", "Switch File Type Filter", CommandKind.System);

    public static readonly CommandDef ShowSettings =
        new("app.settings", "Settings…", CommandKind.User, 6, CommandCategory.App,
            ["preferences", "options", "config"]);
    public static readonly CommandDef SettingsMoveUp = new("settings.move_up", "Settings: Move Up", CommandKind.System);
    public static readonly CommandDef SettingsMoveDown = new("settings.move_down", "Settings: Move Down", CommandKind.System);
    public static readonly CommandDef SettingsActivate = new("settings.activate", "Settings: Change Selected", CommandKind.System);
    public static readonly CommandDef SettingsNextSection = new("settings.next_section", "Settings: Next Section", CommandKind.System);
    public static readonly CommandDef SettingsPreviousSection =
        new("settings.previous_section", "Settings: Previous Section", CommandKind.System);

    public static readonly CommandDef ThemeEditorMoveUp =
        new("settings.theme_editor_move_up", "Custom Theme: Move Up", CommandKind.System);
    public static readonly CommandDef ThemeEditorMoveDown =
        new("settings.theme_editor_move_down", "Custom Theme: Move Down", CommandKind.System);
    public static readonly CommandDef ThemeEditorActivate =
        new("settings.theme_editor_activate", "Custom Theme: Edit Selected", CommandKind.System);
    public static readonly CommandDef ThemeEditorClose =
        new("settings.theme_editor_close", "Custom Theme: Back to Settings", CommandKind.System);
    public static readonly CommandDef ThemeEditorApplyField =
        new("settings.theme_editor_apply_field", "Custom Theme: Apply Color", CommandKind.System);
    public static readonly CommandDef ThemeEditorCancelField =
        new("settings.theme_editor_cancel_field", "Custom Theme: Cancel Edit", CommandKind.System);
}
