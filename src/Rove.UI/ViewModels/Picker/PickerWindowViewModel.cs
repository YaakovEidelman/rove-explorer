using Avalonia;
using Avalonia.Input;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using Rove.UI.Services;
using System.ComponentModel;

namespace Rove.UI.ViewModels;

public sealed partial class PickerWindowViewModel : ViewModelBase
{
    public const int CancelExitCode = 1;

    private readonly CommandRegistry _registry;
    private readonly FileClipboard _clipboard;
    private readonly bool _directoryMode;
    private readonly bool _multiple;
    private readonly string _outputFile;
    private readonly PickerFilter[] _filters;
    private readonly Action<int> _exit;
    private int _filterIndex;

    public ContentViewModel ContentPage { get; }
    public ConfirmViewModel Confirm { get; }
    public FileOperationViewModel FileOperation { get; }

    [ObservableProperty]
    private string _statusError = string.Empty;

    [ObservableProperty]
    private string _statusInfo = string.Empty;

    public string StatusLine =>
        StatusError.Length > 0 ? StatusError
        : StatusInfo.Length > 0 ? StatusInfo
        : ModeHint;

    public bool IsErrorShown => StatusError.Length > 0;

    partial void OnStatusErrorChanged(string value) => RefreshStatusBar();

    partial void OnStatusInfoChanged(string value) => RefreshStatusBar();

    public PickerWindowViewModel(
        CommandRegistry registry,
        ContentViewModel content,
        ConfirmViewModel confirm,
        FileClipboard clipboard,
        FileOperationViewModel fileOperation,
        bool directoryMode,
        bool multiple,
        string outputFile,
        PickerFilter[]? filters = null,
        int filterIndex = 0,
        Action<int>? exit = null)
    {
        _registry = registry;
        _clipboard = clipboard;
        _directoryMode = directoryMode;
        _multiple = multiple;
        _outputFile = outputFile;
        _filters = filters is { Length: > 0 } ? filters : [];
        _exit = exit ?? Environment.Exit;
        _filterIndex = _filters.Length > 0 ? Math.Clamp(filterIndex, 0, _filters.Length - 1) : 0;
        ContentPage = content;
        Confirm = confirm;
        FileOperation = fileOperation;

        _registry.Register(CommandDef.CloseApp, Cancel);
        _registry.Register(CommandDef.ToggleTheme, ToggleTheme);
        _registry.Register(CommandDef.ContentGetItem, ConfirmOrNavigate);
        _registry.Register(CommandDef.EscapeBrowse, Cancel);
        if (_directoryMode)
            _registry.Register(CommandDef.PickerSelectFolder, ConfirmFolder);
        if (_filters.Length > 1)
            _registry.Register(CommandDef.PickerCycleFilter, CycleFilter);

        ContentPage.ErrorRaised += message => StatusError = message;
        ContentPage.InfoRaised += message => StatusInfo = message;
        ContentPage.ConfirmRequested += Confirm.Request;

        FileOperation.InfoRaised += message => StatusInfo = message;
        FileOperation.PropertyChanged += OnSurfaceChanged;
        Confirm.PropertyChanged += OnSurfaceChanged;
        ContentPage.PropertyChanged += OnSurfaceChanged;
        ContentPage.DirectoryListing.PropertyChanged += OnSurfaceChanged;
        _clipboard.Changed += RefreshStatusBar;
    }

    private void OnSurfaceChanged(object? sender, PropertyChangedEventArgs e) => RefreshStatusBar();

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
        Mode.Browse => _directoryMode
            ? (_multiple
                ? "j/k move · Enter open folder · v mark folders · Ctrl+O choose marked (or highlighted) · Esc cancel"
                : "j/k move · Enter open folder · Ctrl+O choose highlighted folder · Esc cancel")
            : "j/k move · Enter choose · v mark multiple · Esc cancel" + (_filters.Length > 1
                ? " · Ctrl+F switch filter"
                : ""),
        Mode.LocalSearch => "type to filter · Enter keeps filter, back to browsing · Esc clears",
        Mode.EditPath => "type a path · Tab complete · Enter go · Esc cancel",
        Mode.PathCompletion => "Ctrl+N/Ctrl+P move · Tab go deeper · Enter go · Esc close list",
        Mode.RenameItem => "Enter apply · Esc cancel",
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
            if (ContentPage.IsAdminView)
                summary += " · administrator (read-only)";
            if (_filters.Length > 0)
                summary += $" · filter: {_filters[_filterIndex].Name}";
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

    private void ConfirmOrNavigate()
    {
        if (ContentPage.HighlightedItem is { Item.IsDirectory: true })
        {
            ContentPage.GetItem();
            return;
        }
        if (!_directoryMode)
            ConfirmSelection();
    }

    private void ConfirmFolder()
    {
        string[] marked = _multiple ? MarkedDirectories() : [];
        WriteResultAndExit(marked.Length > 0 ? marked : [HighlightedFolderOrCurrent()]);
    }

    private string[] MarkedDirectories() =>
    [
        .. ContentPage.DirectoryListing.Items
            .Where(i => i.IsMarked && i.Item.IsDirectory)
            .Select(i => i.Item.FullPath),
    ];

    private string HighlightedFolderOrCurrent() =>
        ContentPage.HighlightedItem is { Item.IsDirectory: true } highlighted
            ? highlighted.Item.FullPath
            : ContentPage.DirectoryListing.CurrentDir;

    private void ConfirmSelection()
    {
        string[] chosen = _multiple ? MarkedOrHighlighted() : Highlighted();
        if (chosen.Length > 0)
            WriteResultAndExit(chosen);
    }

    private string[] MarkedOrHighlighted()
    {
        string[] marked =
        [
            .. ContentPage.DirectoryListing.Items
                .Where(i => i.IsMarked)
                .Select(i => i.Item.FullPath),
        ];
        return marked.Length > 0 ? marked : Highlighted();
    }

    private string[] Highlighted() =>
        ContentPage.HighlightedItem is { } highlighted ? [highlighted.Item.FullPath] : [];

    private void WriteResultAndExit(string[] paths)
    {
        File.WriteAllLines(_outputFile, paths);
        _exit(0);
    }

    private void CycleFilter()
    {
        _filterIndex = (_filterIndex + 1) % _filters.Length;
        ContentPage.DirectoryListing.SetSelectionFilter(_filters[_filterIndex].Patterns);
        RefreshStatusBar();
    }

    public void Cancel() => _exit(CancelExitCode);

    private static void ToggleTheme()
    {
        if (Application.Current is not { } app)
            return;
        app.RequestedThemeVariant =
            app.ActualThemeVariant == ThemeVariant.Dark ? ThemeVariant.Light : ThemeVariant.Dark;
    }
}
