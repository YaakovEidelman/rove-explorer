using Avalonia.Input;

namespace Rove.UI.Services;

public static class KeymapDefaults
{
    private static KeyStroke K(Key key, KeyModifiers mods = KeyModifiers.None) => new(key, mods);

    private static IEnumerable<KommandShortcut> BookmarkShortcuts()
    {
        for (int i = 0; i < BookmarkStore.ShortcutCount; i++)
            yield return new(K(Key.D1 + i, KeyModifiers.Control), CommandDef.BookmarkGo(i).Id);
    }

    private static IEnumerable<KommandShortcut> TabShortcuts()
    {
        for (int i = 0; i < CommandDef.TabShortcutCount; i++)
            yield return new(K(Key.D1 + i, KeyModifiers.Alt), CommandDef.TabGo(i).Id);
    }

    public static readonly Dictionary<Mode, KommandShortcut[]> DefaultModeBindings = new()
    {
        {
            Mode.Browse,
            [
                new(K(Key.Space), CommandDef.TogglePalette.Id),
                new(K(Key.J), CommandDef.ContentMoveDown.Id),
                new(K(Key.K), CommandDef.ContentMoveUp.Id),
                new(K(Key.N, KeyModifiers.Control), CommandDef.ContentMoveDown.Id),
                new(K(Key.P, KeyModifiers.Control), CommandDef.ContentMoveUp.Id),
                new(K(Key.Home), CommandDef.ContentMoveTop.Id),
                new(K(Key.End), CommandDef.ContentMoveBottom.Id),
                new(K(Key.Enter), CommandDef.ContentGetItem.Id),
                new(K(Key.L), CommandDef.ContentRight.Id),
                new(K(Key.H), CommandDef.ContentLeft.Id),
                new(K(Key.Back), CommandDef.ContentGoUpDirectory.Id),
                new(K(Key.H, KeyModifiers.Alt), CommandDef.ContentGoBack.Id),
                new(K(Key.L, KeyModifiers.Alt), CommandDef.ContentGoForward.Id),
                new(K(Key.OemQuestion), CommandDef.ToggleLocalSearch.Id),
                new(K(Key.S), CommandDef.ToggleGlobalSearch.Id),
                new(K(Key.R), CommandDef.ToggleRenameItem.Id),
                new(K(Key.R, KeyModifiers.Shift), CommandDef.ToggleRenameTab.Id),
                new(K(Key.V), CommandDef.ToggleMarkItem.Id),
                new(K(Key.OemPeriod), CommandDef.ToggleShowHidden.Id),
                new(K(Key.Escape), CommandDef.EscapeBrowse.Id),
                new(K(Key.D), CommandDef.DeleteItems.Id),
                new(K(Key.D, KeyModifiers.Shift), CommandDef.DeleteItemsPermanent.Id),
                new(K(Key.Y), CommandDef.CopyItems.Id),
                new(K(Key.X), CommandDef.CutItems.Id),
                new(K(Key.P), CommandDef.PasteItems.Id),
                new(K(Key.C), CommandDef.CopyPath.Id),
                new(K(Key.E), CommandDef.ExtractArchives.Id),
                new(K(Key.Z), CommandDef.CompressItems.Id),
                new(K(Key.U), CommandDef.UndoLastAction.Id),
                new(K(Key.F), CommandDef.ToggleCreateFile.Id),
                new(K(Key.F, KeyModifiers.Shift), CommandDef.ToggleCreateFolder.Id),
                new(K(Key.W), CommandDef.ToggleResizeColumns.Id),
                new(K(Key.I), CommandDef.ToggleContentView.Id),
                new(K(Key.G), CommandDef.ShowDrives.Id),
                new(K(Key.T), CommandDef.ShowTrash.Id),
                new(K(Key.U, KeyModifiers.Shift), CommandDef.RestoreTrashedItems.Id),
                new(K(Key.U, KeyModifiers.Control | KeyModifiers.Shift), CommandDef.RestoreAllTrashedItems.Id),
                new(K(Key.D, KeyModifiers.Control | KeyModifiers.Shift), CommandDef.EmptyTrash.Id),
                new(K(Key.L, KeyModifiers.Control), CommandDef.ToggleEditPath.Id),
                new(K(Key.R, KeyModifiers.Control), CommandDef.TogglePreview.Id),
                new(K(Key.C, KeyModifiers.Control), CommandDef.CancelFileOperation.Id),
                new(K(Key.W, KeyModifiers.Control), CommandDef.CloseTab.Id),
                new(K(Key.Q, KeyModifiers.Control), CommandDef.CloseApp.Id),
                new(K(Key.M), CommandDef.ToggleMaximize.Id),
                new(K(Key.M, KeyModifiers.Shift), CommandDef.MinimizeWindow.Id),
                new(K(Key.U, KeyModifiers.Control), CommandDef.CheckForUpdates.Id),
                new(K(Key.B), CommandDef.ToggleBookmark.Id),
                new(K(Key.B, KeyModifiers.Shift), CommandDef.ShowBookmarks.Id),
                new(K(Key.T, KeyModifiers.Control), CommandDef.NewTab.Id),
                new(K(Key.Enter, KeyModifiers.Control), CommandDef.OpenInNewTab.Id),
                new(K(Key.Tab, KeyModifiers.Control), CommandDef.NextTab.Id),
                new(K(Key.Tab, KeyModifiers.Control | KeyModifiers.Shift), CommandDef.PreviousTab.Id),
                new(K(Key.OemComma), CommandDef.ShowSettings.Id),
                new(K(Key.O, KeyModifiers.Control), CommandDef.PickerSelectFolder.Id),
                new(K(Key.F, KeyModifiers.Control), CommandDef.PickerCycleFilter.Id),
                .. BookmarkShortcuts(),
                .. TabShortcuts(),
            ]
        },
        {
            Mode.Bookmarks,
            [
                new(K(Key.Escape), CommandDef.ShowBookmarks.Id),
                new(K(Key.C, KeyModifiers.Control), CommandDef.ShowBookmarks.Id),
                new(K(Key.P, KeyModifiers.Control), CommandDef.BookmarkMoveUp.Id),
                new(K(Key.N, KeyModifiers.Control), CommandDef.BookmarkMoveDown.Id),
                new(K(Key.Up), CommandDef.BookmarkMoveUp.Id),
                new(K(Key.Down), CommandDef.BookmarkMoveDown.Id),
                new(K(Key.Enter), CommandDef.BookmarkExecute.Id),
                new(K(Key.D, KeyModifiers.Control), CommandDef.RemoveBookmark.Id),
                new(K(Key.Tab), CommandDef.QuickAccessNextTab.Id),
                new(K(Key.Tab, KeyModifiers.Shift), CommandDef.QuickAccessPreviousTab.Id),
                new(K(Key.W, KeyModifiers.Control), CommandDef.CloseTab.Id),
                new(K(Key.Q, KeyModifiers.Control), CommandDef.CloseApp.Id),
                .. BookmarkShortcuts(),
            ]
        },
        {
            Mode.AddBookmark,
            [
                new(K(Key.Escape), CommandDef.CancelAddBookmark.Id),
                new(K(Key.C, KeyModifiers.Control), CommandDef.CancelAddBookmark.Id),
                new(K(Key.Enter), CommandDef.ApplyAddBookmark.Id),
            ]
        },
        {
            Mode.Settings,
            [
                new(K(Key.Escape), CommandDef.ShowSettings.Id),
                new(K(Key.C, KeyModifiers.Control), CommandDef.ShowSettings.Id),
                new(K(Key.OemComma), CommandDef.ShowSettings.Id),
                new(K(Key.P, KeyModifiers.Control), CommandDef.SettingsMoveUp.Id),
                new(K(Key.N, KeyModifiers.Control), CommandDef.SettingsMoveDown.Id),
                new(K(Key.Up), CommandDef.SettingsMoveUp.Id),
                new(K(Key.Down), CommandDef.SettingsMoveDown.Id),
                new(K(Key.J), CommandDef.SettingsMoveDown.Id),
                new(K(Key.K), CommandDef.SettingsMoveUp.Id),
                new(K(Key.Enter), CommandDef.SettingsActivate.Id),
                new(K(Key.Space), CommandDef.SettingsActivate.Id),
                new(K(Key.Tab), CommandDef.QuickAccessNextTab.Id),
                new(K(Key.Tab, KeyModifiers.Shift), CommandDef.QuickAccessPreviousTab.Id),
                new(K(Key.W, KeyModifiers.Control), CommandDef.CloseTab.Id),
                new(K(Key.Q, KeyModifiers.Control), CommandDef.CloseApp.Id),
            ]
        },
        {
            Mode.Palette,
            [
                new(K(Key.Escape), CommandDef.TogglePalette.Id),
                new(K(Key.C, KeyModifiers.Control), CommandDef.TogglePalette.Id),
                new(K(Key.P, KeyModifiers.Control), CommandDef.PaletteMoveUp.Id),
                new(K(Key.N, KeyModifiers.Control), CommandDef.PaletteMoveDown.Id),
                new(K(Key.Up), CommandDef.PaletteMoveUp.Id),
                new(K(Key.Down), CommandDef.PaletteMoveDown.Id),
                new(K(Key.Enter), CommandDef.PaletteExecute.Id),
                new(K(Key.Tab), CommandDef.QuickAccessNextTab.Id),
                new(K(Key.Tab, KeyModifiers.Shift), CommandDef.QuickAccessPreviousTab.Id),
                new(K(Key.W, KeyModifiers.Control), CommandDef.CloseTab.Id),
                new(K(Key.Q, KeyModifiers.Control), CommandDef.CloseApp.Id),
            ]
        },
        {
            Mode.LocalSearch,
            [
                new(K(Key.Escape), CommandDef.ToggleLocalSearch.Id),
                new(K(Key.C, KeyModifiers.Control), CommandDef.ToggleLocalSearch.Id),
                new(K(Key.P, KeyModifiers.Control), CommandDef.ContentMoveUp.Id),
                new(K(Key.N, KeyModifiers.Control), CommandDef.ContentMoveDown.Id),
                new(K(Key.Up), CommandDef.ContentMoveUp.Id),
                new(K(Key.Down), CommandDef.ContentMoveDown.Id),
                new(K(Key.Enter), CommandDef.ApplyLocalSearch.Id),
                new(K(Key.W, KeyModifiers.Control), CommandDef.CloseTab.Id),
                new(K(Key.Q, KeyModifiers.Control), CommandDef.CloseApp.Id),
            ]
        },
        {
            Mode.GlobalSearch,
            [
                new(K(Key.Escape), CommandDef.ToggleGlobalSearch.Id),
                new(K(Key.C, KeyModifiers.Control), CommandDef.ToggleGlobalSearch.Id),
                new(K(Key.P, KeyModifiers.Control), CommandDef.GlobalSearchMoveUp.Id),
                new(K(Key.N, KeyModifiers.Control), CommandDef.GlobalSearchMoveDown.Id),
                new(K(Key.Up), CommandDef.GlobalSearchMoveUp.Id),
                new(K(Key.Down), CommandDef.GlobalSearchMoveDown.Id),
                new(K(Key.Enter), CommandDef.GlobalSearchExecute.Id),
                new(K(Key.Tab), CommandDef.QuickAccessNextTab.Id),
                new(K(Key.Tab, KeyModifiers.Shift), CommandDef.QuickAccessPreviousTab.Id),
                new(K(Key.W, KeyModifiers.Control), CommandDef.CloseTab.Id),
                new(K(Key.Q, KeyModifiers.Control), CommandDef.CloseApp.Id),
            ]
        },
        {
            Mode.RenameItem,
            [
                new(K(Key.Escape), CommandDef.ToggleRenameItem.Id),
                new(K(Key.C, KeyModifiers.Control), CommandDef.ToggleRenameItem.Id),
                new(K(Key.Enter), CommandDef.ApplyRename.Id),
            ]
        },
        {
            Mode.RenameTab,
            [
                new(K(Key.Escape), CommandDef.ToggleRenameTab.Id),
                new(K(Key.C, KeyModifiers.Control), CommandDef.ToggleRenameTab.Id),
                new(K(Key.Enter), CommandDef.ApplyRenameTab.Id),
            ]
        },
        {
            Mode.CreateItem,
            [
                new(K(Key.Escape), CommandDef.CancelCreate.Id),
                new(K(Key.C, KeyModifiers.Control), CommandDef.CancelCreate.Id),
                new(K(Key.Enter), CommandDef.ApplyCreate.Id),
            ]
        },
        {
            Mode.ResizeColumns,
            [
                new(K(Key.H), CommandDef.ColumnShrink.Id),
                new(K(Key.Left), CommandDef.ColumnShrink.Id),
                new(K(Key.L), CommandDef.ColumnGrow.Id),
                new(K(Key.Right), CommandDef.ColumnGrow.Id),
                new(K(Key.H, KeyModifiers.Shift), CommandDef.ColumnShrinkLarge.Id),
                new(K(Key.L, KeyModifiers.Shift), CommandDef.ColumnGrowLarge.Id),
                new(K(Key.Tab), CommandDef.ColumnNext.Id),
                new(K(Key.Tab, KeyModifiers.Shift), CommandDef.ColumnPrev.Id),
                new(K(Key.J), CommandDef.ColumnNext.Id),
                new(K(Key.K), CommandDef.ColumnPrev.Id),
                new(K(Key.N, KeyModifiers.Control), CommandDef.ColumnNext.Id),
                new(K(Key.P, KeyModifiers.Control), CommandDef.ColumnPrev.Id),
                new(K(Key.D0), CommandDef.ColumnResetWidth.Id),
                new(K(Key.S), CommandDef.SortByActiveColumn.Id),
                new(K(Key.Escape), CommandDef.ToggleResizeColumns.Id),
                new(K(Key.Enter), CommandDef.ToggleResizeColumns.Id),
                new(K(Key.C, KeyModifiers.Control), CommandDef.ToggleResizeColumns.Id),
                new(K(Key.W, KeyModifiers.Control), CommandDef.CloseTab.Id),
                new(K(Key.Q, KeyModifiers.Control), CommandDef.CloseApp.Id),
            ]
        },
        {
            Mode.EditPath,
            [
                new(K(Key.Escape), CommandDef.CancelEditPath.Id),
                new(K(Key.C, KeyModifiers.Control), CommandDef.CancelEditPath.Id),
                new(K(Key.Enter), CommandDef.ApplyEditPath.Id),
                new(K(Key.Tab), CommandDef.CompletePath.Id),
                new(K(Key.W, KeyModifiers.Control), CommandDef.CloseTab.Id),
                new(K(Key.Q, KeyModifiers.Control), CommandDef.CloseApp.Id),
            ]
        },
        {
            Mode.PathCompletion,
            [
                new(K(Key.Escape), CommandDef.PathCompleteDismiss.Id),
                new(K(Key.C, KeyModifiers.Control), CommandDef.PathCompleteDismiss.Id),
                new(K(Key.Enter), CommandDef.ApplyEditPath.Id),
                new(K(Key.Tab), CommandDef.CompletePath.Id),
                new(K(Key.N, KeyModifiers.Control), CommandDef.PathCompleteMoveDown.Id),
                new(K(Key.P, KeyModifiers.Control), CommandDef.PathCompleteMoveUp.Id),
                new(K(Key.Down), CommandDef.PathCompleteMoveDown.Id),
                new(K(Key.Up), CommandDef.PathCompleteMoveUp.Id),
                new(K(Key.W, KeyModifiers.Control), CommandDef.CloseTab.Id),
                new(K(Key.Q, KeyModifiers.Control), CommandDef.CloseApp.Id),
            ]
        },
        {
            Mode.Confirm,
            [
                new(K(Key.Y), CommandDef.ConfirmAccept.Id),
                new(K(Key.N), CommandDef.ConfirmCancel.Id),
                new(K(Key.Escape), CommandDef.ConfirmCancel.Id),
                new(K(Key.C, KeyModifiers.Control), CommandDef.ConfirmCancel.Id),
                new(K(Key.Enter), CommandDef.ConfirmSelect.Id),
                new(K(Key.H), CommandDef.ConfirmMoveLeft.Id),
                new(K(Key.L), CommandDef.ConfirmMoveRight.Id),
            ]
        },
    };
}
