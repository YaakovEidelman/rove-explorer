using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Rove.UI.Services;
using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Rove.UI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly CommandRegistry _registry;
    private readonly FileClipboard _clipboard;
    private readonly UpdateService? _updates;

    public TabsViewModel Tabs { get; }

    public ContentViewModel ContentPage => Tabs.Active;

    public PaletteViewModel Palette { get; }
    public GlobalSearchViewModel GlobalSearch { get; }
    public BookmarksViewModel Bookmarks { get; }
    public ConfirmViewModel Confirm { get; }
    public PreviewViewModel Preview { get; }
    public FileOperationViewModel FileOperation { get; }
    public SettingsViewModel Settings { get; }

    [ObservableProperty]
    private string _statusError = string.Empty;

    [ObservableProperty]
    private string _statusInfo = string.Empty;

    public string StatusLine =>
        StatusError.Length > 0 ? StatusError
        : StatusInfo.Length > 0 ? StatusInfo
        : ModeHint;

    public bool IsErrorShown => StatusError.Length > 0;

    public bool IsQuickAccessOpen =>
        Palette.IsPaletteOpen || GlobalSearch.IsOpen || Bookmarks.IsOpen || Settings.IsOpen;

    public string QuickAccessTitle =>
        Palette.IsPaletteOpen ? "COMMANDS"
        : GlobalSearch.IsOpen ? "SEARCH"
        : Bookmarks.IsOpen ? "BOOKMARKS"
        : Settings.IsOpen ? "SETTINGS"
        : "";

    partial void OnStatusErrorChanged(string value) => RefreshStatusBar();

    partial void OnStatusInfoChanged(string value) => RefreshStatusBar();

    public MainWindowViewModel(
        CommandRegistry registry,
        TabsViewModel tabs,
        PaletteViewModel palette,
        GlobalSearchViewModel globalSearch,
        BookmarksViewModel bookmarks,
        ConfirmViewModel confirm,
        PreviewViewModel preview,
        FileClipboard clipboard,
        FileOperationViewModel fileOperation,
        SettingsViewModel settings,
        string? startupWarning = null,
        UpdateService? updates = null
    )
    {
        _registry = registry;
        _clipboard = clipboard;
        _updates = updates;
        Tabs = tabs;
        Palette = palette;
        GlobalSearch = globalSearch;
        Bookmarks = bookmarks;
        Confirm = confirm;
        Preview = preview;
        FileOperation = fileOperation;
        Settings = settings;

        _registry.Register(CommandDef.CloseApp, CloseApp);
        _registry.Register(CommandDef.ToggleTheme, ToggleTheme);
        _registry.Register(CommandDef.ToggleMaximize, ToggleMaximize);
        _registry.Register(CommandDef.MinimizeWindow, MinimizeWindow);
        _registry.Register(CommandDef.CheckForUpdates, CheckForUpdates);
        _registry.Register(CommandDef.QuickAccessNextTab, CycleQuickAccessTab);
        _registry.Register(CommandDef.QuickAccessPreviousTab, CycleQuickAccessTabBackward);

        Tabs.ErrorRaised += message => StatusError = message;
        Tabs.InfoRaised += message => StatusInfo = message;
        Tabs.ConfirmRequested += Confirm.Request;
        Tabs.DrivePickerRequested +=
            () => Palette.OpenScoped(CommandDef.DriveIdPrefix, "pick a drive…");
        Tabs.AppPickerRequested += loading => _ = ShowAppPickerAsync(loading);
        Tabs.SurfaceChanged += RefreshStatusBar;
        Tabs.SelectionChanged += () => Preview.ShowFor(ContentPage.HighlightedItem?.Item, ContentPage.IsAdminView);
        Tabs.ActiveChanged += OnActiveTabChanged;
        Tabs.Emptied += CloseApp;

        GlobalSearch.ErrorRaised += message => StatusError = message;
        GlobalSearch.NavigateRequested += (dir, highlight) =>
            _ = ContentPage.SetCurrentDirectoryAsync(dir, highlight);
        Bookmarks.GoRequested += path => ContentPage.GoToBookmark(path);
        Bookmarks.InfoRaised += message => StatusInfo = message;

        FileOperation.InfoRaised += message => StatusInfo = message;
        FileOperation.PropertyChanged += OnSurfaceChanged;

        if (_updates is not null)
        {
            _updates.InfoRaised += message => Dispatcher.UIThread.Post(() => StatusInfo = message);
            _updates.ErrorRaised += message => Dispatcher.UIThread.Post(() => StatusError = message);
        }

        if (startupWarning is { Length: > 0 })
            StatusError = startupWarning;

        _clipboard.Changed += RefreshStatusBar;

        Palette.PropertyChanged += OnSurfaceChanged;
        Palette.Executing += () => _handingOff = true;
        Palette.Executed += EndHandoff;
        GlobalSearch.PropertyChanged += OnSurfaceChanged;
        Bookmarks.PropertyChanged += OnSurfaceChanged;
        Confirm.PropertyChanged += OnSurfaceChanged;
        Settings.PropertyChanged += OnSurfaceChanged;
    }

    private void OnActiveTabChanged()
    {
        OnPropertyChanged(nameof(ContentPage));
        Preview.ShowFor(ContentPage.HighlightedItem?.Item, ContentPage.IsAdminView);
    }

    private bool _lastQuickAccessOpen;

    private bool _handingOff;

    private void EndHandoff()
    {
        _handingOff = false;
        RefreshSurfaces();
    }

    private async Task ShowAppPickerAsync(Task loading)
    {
        Palette.OpenScoped(CommandDef.OpenWithIdPrefix, "open with…");
        await loading;
        Palette.Refresh();
    }

    private void OnSurfaceChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_handingOff)
            RefreshSurfaces();
    }

    private void RefreshSurfaces()
    {
        RefreshStatusBar();
        bool nowOpen = IsQuickAccessOpen;
        if (nowOpen != _lastQuickAccessOpen)
        {
            _lastQuickAccessOpen = nowOpen;
            OnPropertyChanged(nameof(IsQuickAccessOpen));
        }
        OnPropertyChanged(nameof(QuickAccessTitle));
    }

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
            return Mode.Bookmarks;
        if (Settings.IsOpen)
            return Mode.Settings;
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
            "type to narrow · Ctrl+N/Ctrl+P move · Enter go · Ctrl+D forget · Tab switch tab · Esc close",
        Mode.Settings => "j/k move · Enter/Space change · Tab switch tab · Esc close",
        Mode.EditPath => "type a path · Tab complete · Enter go · Esc cancel",
        Mode.PathCompletion => "Ctrl+N/Ctrl+P move · Tab go deeper · Enter go · Esc close list",
        Mode.RenameItem => "Enter apply · Esc cancel",
        Mode.RenameTab => "Enter apply · Esc cancel",
        Mode.CreateItem => "Enter create · Esc cancel",
        Mode.Confirm => "y/Enter confirm · n/Esc cancel",
        Mode.ResizeColumns => "h/l narrower/wider · Shift for bigger steps · Tab next column · s sort · 0 reset · Esc done",
        _ => "",
    };

    public string ItemSummary
    {
        get
        {
            int total = ContentPage.DirectoryListing.Items.Count;
            int marked = ContentPage.DirectoryListing.Items.Count(i => i.IsMarked);
            string summary = $"{total} item{(total == 1 ? "" : "s")}";
            if (marked > 0)
                summary += $" · {marked} marked";
            if (ContentPage.DirectoryListing.ShowHidden)
                summary += " · hidden shown";
            if (ContentPage.InArchive)
                summary += " · in a zip";
            return summary;
        }
    }

    public string ClipboardSummary
    {
        get
        {
            if (!_clipboard.HasItems)
                return "";
            string verb = _clipboard.Op == ClipboardOp.Copy ? "copied" : "cut";
            return $"{_clipboard.Paths.Count} {verb}";
        }
    }

    private void RefreshStatusBar()
    {
        OnPropertyChanged(nameof(ModeHint));
        OnPropertyChanged(nameof(StatusLine));
        OnPropertyChanged(nameof(IsErrorShown));
        OnPropertyChanged(nameof(ItemSummary));
        OnPropertyChanged(nameof(ClipboardSummary));
    }

    private void CloseApp()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
    }

    private static void ToggleTheme()
    {
        if (Application.Current is not { } app)
            return;
        app.RequestedThemeVariant =
            app.ActualThemeVariant == ThemeVariant.Dark ? ThemeVariant.Light : ThemeVariant.Dark;
    }

    private void CheckForUpdates()
    {
        if (_updates is null)
        {
            StatusError = "Updates aren't available in this build.";
            return;
        }
        _ = _updates.CheckNowAsync(CancellationToken.None);
    }

    private static void ToggleMaximize()
    {
        if (MainWindowInstance() is not { } window)
            return;
        window.WindowState =
            window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private static void MinimizeWindow()
    {
        if (MainWindowInstance() is { } window)
            window.WindowState = WindowState.Minimized;
    }

    private static Window? MainWindowInstance() =>
        Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } window }
            ? window
            : null;
}
