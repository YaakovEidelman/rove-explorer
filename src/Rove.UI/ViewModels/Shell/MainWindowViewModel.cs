using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Rove.UI.Services;
using System.ComponentModel;

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
}
