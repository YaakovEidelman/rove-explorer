using Avalonia.Input;
using Rove.UI.Services;

namespace Rove.UI.ViewModels;

public partial class MainWindowViewModel
{
    public bool HandleKey(Key key, KeyModifiers keyModifiers)
    {
        KeyStroke stroke = new(key, keyModifiers);
        Mode mode = GetCurrentMode();
        StatusError = string.Empty;
        StatusInfo = string.Empty;
        try
        {
            bool handled = _registry.TryExecute(mode, stroke);
            RefreshStatusBar();
            return handled;
        }
        catch (Exception ex)
        {
            StatusError = ex.Message;
            return true;
        }
    }

    public Mode GetCurrentMode()
    {
        if (Confirm.IsOpen)
            return Mode.Confirm;
        if (Palette.IsPaletteOpen)
            return Mode.Palette;
        if (GlobalSearch.IsOpen)
            return Mode.GlobalSearch;
        if (Bookmarks.IsOpen)
        {
            if (Bookmarks.Completions.IsOpen)
                return Mode.AddBookmarkCompletion;
            return Bookmarks.InAddBookmark ? Mode.AddBookmark : Mode.Bookmarks;
        }
        if (Settings.IsOpen)
        {
            if (Settings.InThemeEditor)
                return Settings.InThemeEditorField ? Mode.ThemeEditorField : Mode.ThemeEditor;
            return Mode.Settings;
        }
        if (Tabs.Items.Any(t => t.IsRenaming))
            return Mode.RenameTab;
        if (ContentPage.DirectoryListing.InLocalSearch)
            return Mode.LocalSearch;
        if (ContentPage.Completions.IsOpen)
            return Mode.PathCompletion;
        if (ContentPage.InEditPath)
            return Mode.EditPath;
        if (ContentPage.HighlightedItem is { IsRenaming: true })
            return Mode.RenameItem;
        if (ContentPage.InCreateItem)
            return Mode.CreateItem;
        if (ContentPage.InResizeColumns)
            return Mode.ResizeColumns;
        return Mode.Browse;
    }

    public string ModeHint => GetCurrentMode() switch
    {
        Mode.Browse => "j/k move · Enter open · h up · v mark · Space palette",
        Mode.Palette => "type to filter · Enter run · Tab switch tab · Esc close",
        Mode.LocalSearch => "type to filter · Enter keeps filter, back to browsing · Esc clears",
        Mode.GlobalSearch => "type to search · Enter jump · Tab switch tab · Esc close",
        Mode.Bookmarks =>
            "type to narrow · Ctrl+N/Ctrl+P move · Ctrl+Shift+N/P reorder · Enter go/add · Ctrl+D forget · Tab switch tab · Esc close",
        Mode.AddBookmark => "type a path · Tab complete · Enter add · Esc cancel",
        Mode.AddBookmarkCompletion => "Ctrl+N/Ctrl+P move · Tab go deeper · Enter add · Esc close list",
        Mode.Settings => "j/k move · h/l section · Enter/Space change · Tab switch tab · Esc close",
        Mode.ThemeEditor => "j/k move · Enter edit/toggle · Esc back to settings",
        Mode.ThemeEditorField => "type a hex color · Enter apply · Esc cancel",
        Mode.EditPath => "type a path · Tab complete · Enter go · Esc cancel",
        Mode.PathCompletion => "Ctrl+N/Ctrl+P move · Tab go deeper · Enter go · Esc close list",
        Mode.RenameItem => "Enter apply · Esc cancel",
        Mode.RenameTab => "Enter apply · Esc cancel",
        Mode.CreateItem => "Enter create · Esc cancel",
        Mode.Confirm => "y/Enter confirm · n/Esc cancel",
        Mode.ResizeColumns => "h/l narrower/wider · Shift for bigger steps · Tab next column · s sort · 0 reset · Esc done",
        _ => "",
    };
}
