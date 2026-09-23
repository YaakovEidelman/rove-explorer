using CommunityToolkit.Mvvm.ComponentModel;
using Rove.Core;
using Rove.Core.Protocol;
using Rove.Core.Services;
using Rove.UI.Services;
using Rove.UI.Models;

namespace Rove.UI.ViewModels;

public partial class ContentViewModel : ViewModelBase
{
    private readonly ICommandTarget _registry;
    private readonly RoveCore _core;

    private readonly FileWatchService _watcher;
    private readonly IIconCache _cache;
    private readonly FileClipboard _clipboard;
    private readonly IRoveClipboardService _systemClipboard;
    private readonly FileOperationViewModel _operation;
    private readonly BookmarkStore _bookmarks;
    private readonly SettingsStore _settings;
    private readonly UndoStack _undo;

    public event Action<string>? ErrorRaised;

    public event Action<string>? InfoRaised;

    public event Action<string, Action>? ConfirmRequested;

    public event Action? DrivePickerRequested;

    public event Action<Task>? AppPickerRequested;

    public ContentViewModel(
        ICommandTarget registry,
        RoveCore core,
        FileClipboard clipboard,
        IRoveClipboardService systemClipboard,
        IIconCache cache,
        FileOperationViewModel operation,
        BookmarkStore bookmarks,
        UndoStack undo,
        SettingsStore settings
    )
    {
        _registry = registry;
        _core = core;
        _clipboard = clipboard;
        _systemClipboard = systemClipboard;
        _cache = cache;
        _operation = operation;
        _bookmarks = bookmarks;
        _undo = undo;
        _settings = settings;
        _watcher = core.NewWatcher();
        _directoryListing = new(_cache, _settings);
        Completions = new(_core);
        Completions.Filled += SetEditPathText;

        AppSettings defaults = _settings.Current;
        DirectoryListing.ShowHidden = defaults.ShowHiddenByDefault;
        DirectoryListing.GroupByDate = defaults.GroupByDate;
        if (defaults.DefaultView == "Icons")
            ViewMode = ContentViewMode.Icons;

        _clipboard.Changed += RefreshCutFlags;

        _watcher.Subscribe(
            events => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                DirectoryListing.ApplyBatch(events);
                RefreshCutFlags();
            }),
            () => Avalonia.Threading.Dispatcher.UIThread.Post(() => _ = ReloadCurrentDirectoryAsync())
        );

        RegisterBindings();

        Avalonia.Threading.Dispatcher.UIThread.Post(
            () => _ = SetCurrentDirectoryAsync(DirectoryListing.CurrentDir));
    }

    [ObservableProperty]
    private DirectoryListing _directoryListing;

    public PathCompletionViewModel Completions { get; }

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _inArchive;

    [ObservableProperty]
    private bool _inTrash;

    [ObservableProperty]
    private bool _isAdminView;

    public ListViewItem? HighlightedItem => DirectoryListing.ListSelection.SelectedItem;

    public void Close() => _core.ReleaseWatcher(_watcher);

    private List<ListViewItem> Targets()
    {
        List<ListViewItem> marked = [.. DirectoryListing.Items.Where(i => i.IsMarked)];
        if (marked.Count > 0)
            return marked;
        return HighlightedItem is { } highlighted ? [highlighted] : [];
    }

    private bool RefusedInArchive(string verb)
    {
        if (!InArchive)
            return false;
        InfoRaised?.Invoke($"{verb} does not work inside a zip — extract it first.");
        return true;
    }

    private bool RefusedInTrash(string verb)
    {
        if (!InTrash)
            return false;
        InfoRaised?.Invoke($"{verb} does not work inside the {TrashService.DisplayName}.");
        return true;
    }

    private bool RefusedInAdminView(string verb)
    {
        if (!IsAdminView)
            return false;
        InfoRaised?.Invoke($"{verb} does not work in administrator view yet — it is read-only.");
        return true;
    }

    public string SearchRoot =>
        ArchivePath.TryParse(DirectoryListing.CurrentDir, out ArchivePath inside)
            ? Path.GetDirectoryName(LongPath.Display(inside.Archive)) ?? DirectoryListing.CurrentDir
            : DirectoryListing.CurrentDir;

    private static string Plural(int n) => n == 1 ? "" : "s";

    private static string Describe(int n) => $"{n} item{Plural(n)}";

    private bool StillIn(string directory) => PathCompare.PathMatches(DirectoryListing.CurrentDir, directory);

    private static string DescribeFailure(CommandResult<OpResult[]> result, string what)
    {
        int done = result.Data?.Count(r => r.Ok) ?? 0;
        bool cancelled = result.Reason == "cancelled"
            || (result.Data?.Any(r => r.Reason == "cancelled") ?? false);
        if (cancelled)
            return $"{what} cancelled after {Describe(done)}.";
        return result.Message ?? $"{what} failed.";
    }

    private void RegisterBindings()
    {
        _registry.Register(CommandDef.ContentMoveUp, ContentMoveUp);
        _registry.Register(CommandDef.ContentMoveDown, ContentMoveDown);
        _registry.Register(CommandDef.ContentMoveTop, ContentMoveTop);
        _registry.Register(CommandDef.ContentMoveBottom, ContentMoveBottom);
        _registry.Register(CommandDef.ContentGetItem, GetItem);
        _registry.Register(CommandDef.ContentGoUpDirectory, GoUpDirectory);
        _registry.Register(CommandDef.ContentGoBack, () => _ = GoBackAsync());
        _registry.Register(CommandDef.ContentGoForward, () => _ = GoForwardAsync());
        _registry.Register(CommandDef.ContentLeft, ContentLeft);
        _registry.Register(CommandDef.ContentRight, ContentRight);
        _registry.Register(CommandDef.ToggleEditPath, ToggleEditPath);
        _registry.Register(CommandDef.ApplyEditPath, ApplyEditPath);
        _registry.Register(CommandDef.CancelEditPath, CancelEditPath);
        _registry.Register(CommandDef.CompletePath, CompletePath);
        _registry.Register(CommandDef.PathCompleteMoveUp, Completions.MoveUp);
        _registry.Register(CommandDef.PathCompleteMoveDown, Completions.MoveDown);
        _registry.Register(CommandDef.PathCompleteDismiss, DismissCompletions);
        _registry.Register(CommandDef.ShowDrives, ShowDrives);
        _registry.Register(CommandDef.OpenWith, ShowOpenWith,
            canRun: () => !InArchive && !InTrash && !IsAdminView
                && HighlightedItem is { } highlighted && !highlighted.Item.IsDirectory);
        _registry.Register(CommandDef.OpenTerminal, OpenTerminalHere, canRun: () => !InArchive && !InTrash);
        _registry.Register(CommandDef.ShowTrash, GoToTrash);
        _registry.Register(CommandDef.RestoreTrashedItems, RestoreTrashedItems, canRun: () => InTrash);
        _registry.Register(CommandDef.RestoreAllTrashedItems, RestoreAllTrashedItems, canRun: () => InTrash);
        _registry.Register(CommandDef.EmptyTrash, EmptyTrash, canRun: () => InTrash);
        _registry.Register(CommandDef.ToggleLocalSearch, ToggleLocalSearch);
        _registry.Register(CommandDef.ApplyLocalSearch, LeaveLocalSearchTyping);
        _registry.Register(CommandDef.ToggleRenameItem, ToggleRenameItem,
            canRun: () => !InArchive && !InTrash && !IsAdminView);
        _registry.Register(CommandDef.ApplyRename, ApplyRename);
        _registry.Register(CommandDef.ToggleResizeColumns, ToggleResizeColumns);
        _registry.Register(CommandDef.ColumnNext, ColumnNext);
        _registry.Register(CommandDef.ColumnPrev, ColumnPrev);
        _registry.Register(CommandDef.ColumnGrow, ColumnGrow);
        _registry.Register(CommandDef.ColumnShrink, ColumnShrink);
        _registry.Register(CommandDef.ColumnGrowLarge, ColumnGrowLarge);
        _registry.Register(CommandDef.ColumnShrinkLarge, ColumnShrinkLarge);
        _registry.Register(CommandDef.ColumnResetWidth, ColumnResetWidth);
        _registry.Register(CommandDef.SortByActiveColumn, SortByActiveColumn);
        _registry.Register(CommandDef.SortByName, () => SortBy(SortKey.Name));
        _registry.Register(CommandDef.SortByType, () => SortBy(SortKey.Type));
        _registry.Register(CommandDef.SortBySize, () => SortBy(SortKey.Size));
        _registry.Register(CommandDef.SortByModified, () => SortBy(SortKey.Modified));
        _registry.Register(CommandDef.ToggleContentView, CycleContentView);
        _registry.Register(CommandDef.ToggleGroupByDate, ToggleGroupByDate);
        _registry.Register(CommandDef.ToggleMarkItem, ToggleMarkItem);
        _registry.Register(CommandDef.ClearMarks, ClearMarks);
        _registry.Register(CommandDef.EscapeBrowse, EscapeBrowse);
        _registry.Register(CommandDef.ToggleShowHidden, ToggleShowHidden);
        _registry.Register(CommandDef.DeleteItems, DeleteItems, canRun: () => !InArchive && !IsAdminView && !InTrash);
        _registry.Register(CommandDef.DeleteItemsPermanent, DeleteItemsPermanent, canRun: () => !InArchive && !IsAdminView);
        _registry.Register(CommandDef.CopyItems, CopyItems, canRun: () => !InArchive && !IsAdminView);
        _registry.Register(CommandDef.CutItems, CutItems, canRun: () => !InArchive && !InTrash && !IsAdminView);
        _registry.Register(CommandDef.PasteItems, PasteItems, canRun: () => !InArchive && !InTrash && !IsAdminView);
        _registry.Register(CommandDef.CopyPath, CopyPath);
        _registry.Register(CommandDef.ExtractArchives, ExtractItems,
            canRun: () => !InArchive && !InTrash && !IsAdminView && HasArchiveTarget());
        _registry.Register(CommandDef.CompressItems, CompressItems, canRun: () => !InArchive && !InTrash && !IsAdminView);
        _registry.Register(CommandDef.ToggleBookmark, ToggleBookmark);

        for (int slot = 0; slot < BookmarkStore.ShortcutCount; slot++)
        {
            int index = slot;
            _registry.Register(CommandDef.BookmarkGo(index), () => GoToBookmarkAt(index));
        }
        _registry.Register(CommandDef.UndoLastAction, UndoLastAction);
        _registry.Register(CommandDef.ToggleCreateFile, ToggleCreateFile,
            canRun: () => !InArchive && !InTrash && !IsAdminView);
        _registry.Register(CommandDef.ToggleCreateFolder, ToggleCreateFolder,
            canRun: () => !InArchive && !InTrash && !IsAdminView);
        _registry.Register(CommandDef.ApplyCreate, ApplyCreate);
        _registry.Register(CommandDef.CancelCreate, CancelCreate);
        _registry.Register(CommandDef.CancelFileOperation, _operation.Cancel, canRun: () => _operation.IsRunning);
    }
}
