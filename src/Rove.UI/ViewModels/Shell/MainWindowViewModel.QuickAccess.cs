using Rove.UI.Services;

namespace Rove.UI.ViewModels;

public partial class MainWindowViewModel
{
    private static readonly Mode[] QuickAccessTabs =
        [Mode.Palette, Mode.GlobalSearch, Mode.Bookmarks, Mode.Settings];

    private void CycleQuickAccessTab()
    {
        Mode current = GetCurrentMode();
        int index = Array.IndexOf(QuickAccessTabs, current);
        if (index < 0)
            return;
        OpenQuickAccessTab(QuickAccessTabs[(index + 1) % QuickAccessTabs.Length]);
    }

    private void CycleQuickAccessTabBackward()
    {
        Mode current = GetCurrentMode();
        int index = Array.IndexOf(QuickAccessTabs, current);
        if (index < 0)
            return;
        OpenQuickAccessTab(QuickAccessTabs[(index - 1 + QuickAccessTabs.Length) % QuickAccessTabs.Length]);
    }

    private void OpenQuickAccessTab(Mode target)
    {
        switch (target)
        {
            case Mode.Palette when !Palette.IsPaletteOpen: Palette.TogglePalette(); break;
            case Mode.GlobalSearch when !GlobalSearch.IsOpen: GlobalSearch.Toggle(); break;
            case Mode.Bookmarks when !Bookmarks.IsOpen: Bookmarks.Toggle(); break;
            case Mode.Settings when !Settings.IsOpen: Settings.Toggle(); break;
        }

        if (Palette.IsPaletteOpen && target != Mode.Palette)
            Palette.TogglePalette();
        if (GlobalSearch.IsOpen && target != Mode.GlobalSearch)
            GlobalSearch.Toggle();
        if (Bookmarks.IsOpen && target != Mode.Bookmarks)
            Bookmarks.Toggle();
        if (Settings.IsOpen && target != Mode.Settings)
            Settings.Toggle();
    }
}
