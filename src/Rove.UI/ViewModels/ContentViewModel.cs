using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rove.Core;
using Rove.Core.Protocol;
using Rove.Core.Services;
using Rove.UI.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Rove.UI.Models;

namespace Rove.UI.ViewModels;

public partial class ContentViewModel : ViewModelBase
{
    private readonly ICommandTarget _registry;
    private readonly RoveCore _core;

    /// <summary>
    /// This view's own watcher. One per view rather than one for the app:
    /// a watcher follows a single directory, and two tabs are hardly ever
    /// looking at the same one.
    /// </summary>
    private readonly FileWatchService _watcher;
    private readonly IIconCache _cache;
    private readonly FileClipboard _clipboard;
    private readonly IRoveClipboardService _systemClipboard;
    private readonly FileOperationViewModel _operation;
    private readonly BookmarkStore _bookmarks;
    private readonly SettingsStore _settings;
    /// <summary>
    /// Shared by every tab. Copying in one and undoing from another is a
    /// normal thing to want — the last thing done was done to the disk, not
    /// to a tab, and the tab it happened to be done from is rarely what a
    /// person is thinking about when they reach for undo.
    /// </summary>
    private readonly UndoStack _undo;

    /// <summary>Something went wrong; MainWindow shows it in the status bar.</summary>
    public event Action<string>? ErrorRaised;

    /// <summary>Neutral feedback ("Sent 3 items to the Recycle Bin").</summary>
    public event Action<string>? InfoRaised;

    /// <summary>A destructive verb wants a visible confirm surface before running.</summary>
    public event Action<string, Action>? ConfirmRequested;

    /// <summary>Asks for the drive list to be put in front of the user.</summary>
    public event Action? DrivePickerRequested;

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

        AppSettings defaults = _settings.Current;
        DirectoryListing.ShowHidden = defaults.ShowHiddenByDefault;
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

        // Queued rather than started here: the constructor runs while the app
        // is still being built, before the UI loop owns the thread, and the
        // first listing would otherwise finish on a background thread and
        // hand the rows their icons where no binding is listening.
        Avalonia.Threading.Dispatcher.UIThread.Post(
            () => _ = SetCurrentDirectoryAsync(DirectoryListing.CurrentDir));
    }

    [ObservableProperty]
    private DirectoryListing _directoryListing;

    /// <summary>The Tab-completion list that drops out of the path bar.</summary>
    public PathCompletionViewModel Completions { get; }

    [ObservableProperty]
    private ObservableCollection<FolderViewColumn> _columns = ColumnDefaults.Create();

    private const int ListIconPixels = 16;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsListView))]
    [NotifyPropertyChangedFor(nameof(IsIconView))]
    private ContentViewMode _viewMode = ContentViewMode.List;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IconCellWidth))]
    [NotifyPropertyChangedFor(nameof(IconCellHeight))]
    [NotifyPropertyChangedFor(nameof(IconPixelSize))]
    private IconSize _iconSize = IconSize.Small;

    public bool IsListView => ViewMode == ContentViewMode.List;
    public bool IsIconView => ViewMode == ContentViewMode.Icons;

    public double IconCellWidth => IconSizes.CellWidth(IconSize);
    public double IconCellHeight => IconSizes.CellHeight(IconSize);
    public double IconPixelSize => IconSizes.PixelsFor(IconSize);

    partial void OnViewModeChanged(ContentViewMode value) => ApplyIconSize();
    partial void OnIconSizeChanged(IconSize value) => ApplyIconSize();

    private void ApplyIconSize() =>
        DirectoryListing.SetIconSize(ViewMode == ContentViewMode.List ? ListIconPixels : IconSizes.PixelsFor(IconSize));

    /// <summary>
    /// One key cycles the whole thing: list, then each icon size, then back
    /// to list — three sizes is plenty, and a single key means nothing to
    /// remember beyond "press it again".
    /// </summary>
    private void CycleContentView()
    {
        if (ViewMode == ContentViewMode.List)
        {
            ViewMode = ContentViewMode.Icons;
            IconSize = IconSize.Small;
        }
        else if (IconSize == IconSize.Small)
            IconSize = IconSize.Medium;
        else if (IconSize == IconSize.Medium)
            IconSize = IconSize.Large;
        else
            ViewMode = ContentViewMode.List;

        InfoRaised?.Invoke(IsListView ? "List view." : $"Icon view ({IconSize.ToString().ToLowerInvariant()}).");
    }

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _inCreateItem;

    [ObservableProperty]
    private string _createItemText = string.Empty;

    [ObservableProperty]
    private string _createItemLabel = "New file";

    [ObservableProperty]
    private bool _inResizeColumns;

    [ObservableProperty]
    private bool _inEditPath;

    /// <summary>
    /// Whether what is listed is the inside of a zip rather than a folder on
    /// disk. Worked out once per move, when the path is already in hand,
    /// because answering it means asking the filesystem whether a step of
    /// the path is really a file.
    /// </summary>
    [ObservableProperty]
    private bool _inArchive;

    [ObservableProperty]
    private string _editPathText = string.Empty;

    /// <summary>
    /// Where the cursor sits in the path bar. Bound so that text put there by
    /// a completion leaves the cursor after it, ready to keep typing.
    /// </summary>
    [ObservableProperty]
    private int _editPathCaret;

    /// <summary>
    /// Set while a completion is writing the path bar, so its own write is
    /// not mistaken for the user typing and used to narrow the list again.
    /// </summary>
    private bool _completionIsWriting;

    partial void OnEditPathTextChanged(string value)
    {
        if (_completionIsWriting)
            return;
        _ = Completions.NarrowAsync(value, DirectoryListing.CurrentDir);
    }

    partial void OnInEditPathChanged(bool value)
    {
        if (!value)
            Completions.Close();
    }

    private bool _createIsFolder;

    private int _activeColumnIndex;

    /// <summary>The column the width keys act on while the resize bar is up.</summary>
    public FolderViewColumn? ActiveColumn =>
        _activeColumnIndex >= 0 && _activeColumnIndex < Columns.Count ? Columns[_activeColumnIndex] : null;

    public ListViewItem? HighlightedItem => DirectoryListing.ListSelection.SelectedItem;

    /// <summary>Gives back what this view was holding — its watcher, and nothing else.</summary>
    public void Close() => _core.ReleaseWatcher(_watcher);

    // ── target resolution ────────────────────────────────────────────────
    // Verbs act on the marked set when one exists, else on the highlight.
    // One rule for every multi-item verb, so `d` can never delete something
    // other than what the user is looking at.

    private List<ListViewItem> Targets()
    {
        List<ListViewItem> marked = [.. DirectoryListing.Items.Where(i => i.IsMarked)];
        if (marked.Count > 0)
            return marked;
        return HighlightedItem is { } highlighted ? [highlighted] : [];
    }

    // ── what a zip does not allow ────────────────────────────────────────

    /// <summary>
    /// Verbs that write are refused while the list is showing the inside of
    /// a zip. Rove reads archives and takes things out of them; it does not
    /// edit one in place, and a verb that half-worked would be worse than
    /// one that says plainly it does not apply here.
    /// </summary>
    private bool RefusedInArchive(string verb)
    {
        if (!InArchive)
            return false;
        InfoRaised?.Invoke($"{verb} does not work inside a zip — extract it first.");
        return true;
    }

    /// <summary>
    /// Where a deep search starts. Inside a zip there is no tree on disk to
    /// walk, so the search runs from the folder the archive itself sits in.
    /// </summary>
    public string SearchRoot =>
        ArchivePath.TryParse(DirectoryListing.CurrentDir, out ArchivePath inside)
            ? Path.GetDirectoryName(LongPath.Display(inside.Archive)) ?? DirectoryListing.CurrentDir
            : DirectoryListing.CurrentDir;

    // ── navigation ───────────────────────────────────────────────────────

    public async Task SetCurrentDirectoryAsync(string directory, string? highlightPath = null)
    {
        string from = DirectoryListing.CurrentDir;
        IsLoading = true;
        CommandResult<FolderItem[]> result;
        try
        {
            result = await _core.Actions.ReadDirectoryAsync(new(directory));
        }
        finally
        {
            IsLoading = false;
        }

        if (!result.IsOk || result.Data is null)
        {
            ErrorRaised?.Invoke(result.Message ?? $"Could not open {directory}.");
            return;
        }

        DirectoryListing.InLocalSearch = false;
        DirectoryListing.SearchCurrentDirectoryText = string.Empty;
        DirectoryListing.Load(directory, result.Data);
        DirectoryListing.CurrentDir = directory;
        InArchive = ArchivePath.IsInside(directory);
        RefreshCutFlags();

        if (!_navigatingHistory && !PathCompare.PathMatches(from, directory))
        {
            _backHistory.Add(from);
            _forwardHistory.Clear();
            NotifyHistory();
        }

        if (highlightPath is not null)
            DirectoryListing.ListSelection.SelectPath(highlightPath);
        else
            DirectoryListing.ListSelection.Select(0);

        try
        {
            // There is nothing to watch inside a zip: the entries are not
            // files the system can report on, and the archive changing under
            // us is rare enough to leave to a manual reload.
            if (InArchive)
                _watcher.Pause();
            else
                _watcher.ChangePath(directory);
        }
        catch (Exception)
        {
            // Directory listed fine but can't be watched (rare) — degrade to
            // manual refresh rather than failing navigation.
        }
    }

    public Task ReloadCurrentDirectoryAsync() =>
        SetCurrentDirectoryAsync(DirectoryListing.CurrentDir, HighlightedItem?.Item.FullPath);

    public void GetItem()
    {
        DirectoryListing.FlushPendingFilter();
        if (HighlightedItem is not { } selected)
            return;
        FolderItem item = selected.Item;
        if (item.IsDirectory)
        {
            _ = SetCurrentDirectoryAsync(item.FullPath);
            return;
        }
        ClearLocalSearch();
        if (ArchiveService.IsArchive(item.FullPath))
        {
            _ = OpenArchiveAsync(item);
            return;
        }
        if (InArchive)
        {
            _ = OpenFromArchiveAsync(item);
            return;
        }
        _ = LaunchAsync(item);
    }

    // ── zip files, which open like folders ───────────────────────────────
    // Enter on a .zip walks into it instead of handing it to the system: a
    // zip is somewhere to look through, and looking through one is faster
    // than unpacking it to find out it held the wrong thing.

    /// <summary>
    /// Goes into an archive. One already inside another has to come out to a
    /// real file first — a zip within a zip is not something that can be
    /// read where it lies.
    /// </summary>
    private async Task OpenArchiveAsync(FolderItem item)
    {
        string path = item.FullPath;
        if (InArchive)
        {
            if (await CopyOutAsync(item) is not { } copy)
                return;
            path = copy;
        }
        await SetCurrentDirectoryAsync(path);
    }

    /// <summary>
    /// Opens a file that is inside an archive, by way of a copy taken out of
    /// it. Said out loud, because a copy that is edited and then lost is a
    /// worse surprise than not being able to edit it at all.
    /// </summary>
    private async Task OpenFromArchiveAsync(FolderItem item)
    {
        if (await CopyOutAsync(item) is not { } copy)
            return;
        InfoRaised?.Invoke($"Opened a copy of {item.Name} — edits to it are not saved back into the zip.");
        await LaunchAsync(FolderItem.FromPath(copy));
    }

    /// <summary>
    /// Extracting is real disk I/O, not the instant kind — worth the same
    /// "Loading…" a folder read gets, and something a test can wait on
    /// instead of racing a fire-and-forget task blind.
    /// </summary>
    private async Task<string?> CopyOutAsync(FolderItem item)
    {
        IsLoading = true;
        try
        {
            CommandResult<string?> result = await _core.Actions.CopyOutOfArchiveAsync(new(item.FullPath));
            if (result is { IsOk: true, Data: { Length: > 0 } path })
                return path;
            ErrorRaised?.Invoke(result.Message ?? $"Could not read {item.Name} out of the zip.");
            return null;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Opening a file is handed off and answered for later: the program that
    /// knows the file associations takes a moment to say it has nothing for
    /// this kind of file, and the list should not sit still while it thinks.
    /// </summary>
    private async Task LaunchAsync(FolderItem item)
    {
        CommandResult<string?> result = await _core.Actions.LaunchFileAsync(new(item.FullPath));
        if (!result.IsOk)
            ErrorRaised?.Invoke(result.Message ?? "Could not open the file.");
    }

    private readonly List<string> _backHistory = [];
    private readonly List<string> _forwardHistory = [];
    private bool _navigatingHistory;

    public bool CanGoBack => _backHistory.Count > 0;
    public bool CanGoForward => _forwardHistory.Count > 0;

    private void NotifyHistory()
    {
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(CanGoForward));
    }

    [RelayCommand]
    public async Task GoBackAsync()
    {
        if (_backHistory.Count == 0)
            return;
        string target = _backHistory[^1];
        string current = DirectoryListing.CurrentDir;
        await NavigateHistoryAsync(target);
        if (PathCompare.PathMatches(DirectoryListing.CurrentDir, target))
        {
            _backHistory.RemoveAt(_backHistory.Count - 1);
            _forwardHistory.Add(current);
            NotifyHistory();
        }
    }

    [RelayCommand]
    public async Task GoForwardAsync()
    {
        if (_forwardHistory.Count == 0)
            return;
        string target = _forwardHistory[^1];
        string current = DirectoryListing.CurrentDir;
        await NavigateHistoryAsync(target);
        if (PathCompare.PathMatches(DirectoryListing.CurrentDir, target))
        {
            _forwardHistory.RemoveAt(_forwardHistory.Count - 1);
            _backHistory.Add(current);
            NotifyHistory();
        }
    }

    private async Task NavigateHistoryAsync(string target)
    {
        _navigatingHistory = true;
        try
        {
            await SetCurrentDirectoryAsync(target);
        }
        finally
        {
            _navigatingHistory = false;
        }
    }

    public void GoUpDirectory()
    {
        string current = DirectoryListing.CurrentDir;
        CommandResult<FolderItem?> parent = _core.Actions.GetParent(new(current));
        if (!parent.IsOk)
        {
            ErrorRaised?.Invoke(parent.Message ?? "Could not read the parent directory.");
            return;
        }
        if (parent.Data is null)
        {
            ShowDrives(); // at the top of a drive, "up" is the drive list
            return;
        }
        // Highlight where we came from, so "up" keeps you oriented.
        _ = SetCurrentDirectoryAsync(parent.Data.FullPath, highlightPath: current);
    }

    // ── the path bar ─────────────────────────────────────────────────────
    // The path at the top is also the way in: it opens as a text box holding
    // where you are, and takes anything a shell would take — an absolute
    // path, a relative one, ~, or an environment variable. A path naming a
    // file opens the folder around it with that file highlighted.

    private void ToggleEditPath()
    {
        if (InEditPath)
        {
            InEditPath = false;
            return;
        }
        SetEditPathText(DirectoryListing.CurrentDir);
        InEditPath = true;
    }

    private void CancelEditPath() => InEditPath = false;

    // ── completing what is typed there ───────────────────────────────────
    // Tab finishes the name against what is really in the folder. Where more
    // than one thing matches it carries the text as far as they agree and
    // drops a list out, which is Mode.PathCompletion until it is taken or
    // dismissed. Every one of these writes the box through SetEditPathText,
    // so the list is never re-narrowed by a change it made itself.

    private void CompletePath() => _ = CompletePathAsync();

    private async Task CompletePathAsync()
    {
        if (await Completions.ExpandAsync(EditPathText, DirectoryListing.CurrentDir) is { } completed)
            SetEditPathText(completed);
    }

    private void AcceptCompletion()
    {
        if (Completions.AcceptSelected(EditPathText) is { } taken)
            SetEditPathText(taken);
    }

    private void DismissCompletions() => Completions.Close();

    /// <summary>Puts text in the path bar with the cursor left at the end of it.</summary>
    private void SetEditPathText(string text)
    {
        _completionIsWriting = true;
        try
        {
            EditPathText = text;
            EditPathCaret = text.Length;

            // Said again in case it did not change: the box moves its own
            // cursor as the user clicks around, and a value that matches the
            // one already held here would otherwise never be pushed back.
            OnPropertyChanged(nameof(EditPathCaret));
        }
        finally
        {
            _completionIsWriting = false;
        }
    }

    private void ApplyEditPath() => _ = ApplyEditPathAsync();

    private async Task ApplyEditPathAsync()
    {
        string typed = EditPathText;
        if (typed.Trim().Length == 0)
        {
            InEditPath = false;
            return;
        }

        CommandResult<FolderItem?> found = _core.Actions.ResolvePath(new(typed, DirectoryListing.CurrentDir));
        if (!found.IsOk || found.Data is null)
        {
            // Leave the box open, with the text in it, so it can be corrected.
            ErrorRaised?.Invoke(found.Message ?? $"Could not go to {typed}.");
            return;
        }

        InEditPath = false;
        FolderItem item = found.Data;
        if (item.IsDirectory)
        {
            await SetCurrentDirectoryAsync(item.FullPath);
            return;
        }

        if (Path.GetDirectoryName(item.FullPath) is not { Length: > 0 } parent)
        {
            ErrorRaised?.Invoke($"Could not go to {item.FullPath}.");
            return;
        }
        await SetCurrentDirectoryAsync(parent, highlightPath: item.FullPath);
    }

    // ── drives ───────────────────────────────────────────────────────────
    // Each mounted drive is an ordinary command ("Go to Drive D:\"), so the
    // palette is the drive picker. They are rebuilt every time the palette
    // opens: drives get plugged in and pulled out while the app runs.

    private void ShowDrives() => DrivePickerRequested?.Invoke();

    public void RefreshDriveCommands()
    {
        CommandResult<DriveEntry[]> result = _core.Actions.ListDrives(new());
        if (!result.IsOk)
        {
            ErrorRaised?.Invoke(result.Message ?? "Could not read the drive list.");
            return;
        }

        HashSet<string> live = [];
        foreach (DriveEntry drive in result.Data ?? [])
        {
            string id = CommandDef.DriveIdPrefix + drive.RootPath;
            string root = drive.RootPath;
            string title = drive.Label is null
                ? $"Go to Drive {root}"
                : $"Go to Drive {root} ({drive.Label})";
            live.Add(id);
            _registry.Register(new CommandDef(id, title, CommandKind.User),
                () => _ = SetCurrentDirectoryAsync(root));
        }

        foreach (string id in _registry.CommandIdsStartingWith(CommandDef.DriveIdPrefix))
        {
            if (!live.Contains(id))
                _registry.Unregister(id);
        }
    }

    public void ContentMoveUp() => DirectoryListing.ListSelection.MoveUp();
    public void ContentMoveDown() => DirectoryListing.ListSelection.MoveDown();
    public void ContentMoveTop() => DirectoryListing.ListSelection.MoveTop();
    public void ContentMoveBottom() => DirectoryListing.ListSelection.MoveBottom();

    /// <summary>
    /// h/l. In the list view they are the old ranger-style "up a directory" /
    /// "open" — in the icon view that reads as a stray directory change or a
    /// launched file where the user only meant to move the highlight, so
    /// there they step through the grid instead. Backspace and Enter still
    /// go up and open either way.
    /// </summary>
    private void ContentLeft()
    {
        if (IsIconView)
            DirectoryListing.ListSelection.MoveLeft();
        else
            GoUpDirectory();
    }

    private void ContentRight()
    {
        if (IsIconView)
            DirectoryListing.ListSelection.MoveRight();
        else
            GetItem();
    }

    // ── local search (filter) ────────────────────────────────────────────

    private void ToggleLocalSearch()
    {
        DirectoryListing.FlushPendingFilter();
        DirectoryListing.SearchCurrentDirectoryText = string.Empty;
        DirectoryListing.InLocalSearch = !DirectoryListing.InLocalSearch;
        DirectoryListing.ApplyView();
    }

    /// <summary>
    /// Enter's job while typing: stop editing the query but leave the
    /// filtered view up — closing the box is not the same as giving up the
    /// filter, which stays until something (opening an item, Esc) clears it.
    /// </summary>
    private void LeaveLocalSearchTyping()
    {
        DirectoryListing.FlushPendingFilter();
        DirectoryListing.InLocalSearch = false;
    }

    /// <summary>No-op once there's nothing to clear, so callers can reach for it freely.</summary>
    private void ClearLocalSearch()
    {
        if (!DirectoryListing.InLocalSearch && DirectoryListing.SearchCurrentDirectoryText.Length == 0)
            return;
        DirectoryListing.InLocalSearch = false;
        DirectoryListing.SearchCurrentDirectoryText = string.Empty;
        DirectoryListing.ApplyView();
    }

    /// <summary>What Esc does in Browse: drop marks, then drop a filter left over from search.</summary>
    private void EscapeBrowse()
    {
        ClearMarks();
        ClearLocalSearch();
    }

    // ── rename ───────────────────────────────────────────────────────────

    private void ToggleRenameItem()
    {
        if (RefusedInArchive("Rename"))
            return;
        if (HighlightedItem is not { } selected)
            return;
        selected.EditText = selected.Name;
        selected.IsRenaming = !selected.IsRenaming;
    }

    private void ApplyRename()
    {
        if (HighlightedItem is not { } selected)
            return;

        string newName = selected.EditText.Trim();
        if (newName.Length == 0 || newName == selected.Name)
        {
            selected.IsRenaming = false;
            return;
        }

        CommandResult<FolderItem?> result = _core.Actions.RenameItem(new(selected.Item.FullPath, newName));
        if (!result.IsOk || result.Data is null)
        {
            // Keep the edit box open so the name can be fixed.
            ErrorRaised?.Invoke(result.Message ?? "Rename failed.");
            return;
        }

        string oldPath = selected.Item.FullPath;
        string oldName = selected.Name;
        selected.IsRenaming = false;
        DirectoryListing.Rename(oldPath, result.Data);
        DirectoryListing.ListSelection.SelectPath(result.Data.FullPath);
        _undo.Push(new(UndoAction.RenameBack, $"rename of {oldName}",
            [new PathPair(oldPath, result.Data.FullPath)]));
    }

    // ── column widths ────────────────────────────────────────────────────
    // Widths are keyboard-only: the bar at the bottom is the visible surface,
    // and the highlighted header says which column the keys are pointed at.

    private const double WidthStep = 12;
    private const double WidthStepLarge = 48;

    private void ToggleResizeColumns()
    {
        InResizeColumns = !InResizeColumns;
        if (InResizeColumns)
            PointAt(FirstVisibleColumn());
        else
            PointAt(-1);
    }

    private void ColumnGrow() => ActiveColumn?.ResizeBy(WidthStep);
    private void ColumnShrink() => ActiveColumn?.ResizeBy(-WidthStep);
    private void ColumnGrowLarge() => ActiveColumn?.ResizeBy(WidthStepLarge);
    private void ColumnShrinkLarge() => ActiveColumn?.ResizeBy(-WidthStepLarge);
    private void ColumnResetWidth() => ActiveColumn?.ResetWidth();

    /// <summary>Same order as <see cref="ColumnDefaults.Create"/> — one sort key per column.</summary>
    private static readonly SortKey[] _columnSortKeys = [SortKey.Name, SortKey.Type, SortKey.Size, SortKey.Modified];

    private void SortByActiveColumn()
    {
        if (_activeColumnIndex < 0 || _activeColumnIndex >= _columnSortKeys.Length)
            return;
        DirectoryListing.SetSort(_columnSortKeys[_activeColumnIndex]);
    }

    private void ColumnNext() => StepActiveColumn(1);
    private void ColumnPrev() => StepActiveColumn(-1);

    /// <summary>Walks to the next visible column, wrapping at either end.</summary>
    private void StepActiveColumn(int direction)
    {
        if (Columns.Count == 0)
            return;
        int index = _activeColumnIndex;
        for (int step = 0; step < Columns.Count; step++)
        {
            index = (index + direction + Columns.Count) % Columns.Count;
            if (Columns[index].IsVisible)
            {
                PointAt(index);
                return;
            }
        }
    }

    private int FirstVisibleColumn()
    {
        for (int i = 0; i < Columns.Count; i++)
        {
            if (Columns[i].IsVisible)
                return i;
        }
        return -1;
    }

    private void PointAt(int index)
    {
        _activeColumnIndex = index;
        for (int i = 0; i < Columns.Count; i++)
            Columns[i].IsActive = i == index;
        OnPropertyChanged(nameof(ActiveColumn));
    }

    // ── hidden items ─────────────────────────────────────────────────────

    private void ToggleShowHidden()
    {
        DirectoryListing.ShowHidden = !DirectoryListing.ShowHidden;
        InfoRaised?.Invoke(DirectoryListing.ShowHidden
            ? "Showing hidden items."
            : "Hiding hidden items.");
    }

    // ── marks ────────────────────────────────────────────────────────────

    private void ToggleMarkItem()
    {
        if (HighlightedItem is not { } selected)
            return;
        selected.IsMarked = !selected.IsMarked;
    }

    private void ClearMarks()
    {
        foreach (ListViewItem item in DirectoryListing.Items)
            item.IsMarked = false;
    }

    // ── delete ───────────────────────────────────────────────────────────

    private void DeleteItems()
    {
        if (!RefusedInArchive("Delete"))
            _ = DeleteItemsAsync();
    }

    private async Task DeleteItemsAsync()
    {
        List<ListViewItem> targets = Targets();
        if (targets.Count == 0)
            return;

        string[] paths = [.. targets.Select(t => t.Item.FullPath)];
        string from = DirectoryListing.CurrentDir;
        ClearMarks();

        // The Windows Recycle Bin call is one shell batch that can't report or stop
        // partway, so the bar shows movement rather than a count.
        CommandResult<OpResult[]>? outcome = await _operation.RunAsync(
            $"Deleting {Describe(targets.Count)}",
            indeterminate: true,
            (progress, ct) => _core.Actions.DeleteItemsAsync(new(paths), progress, ct));

        if (outcome is not { } result)
            return;
        if (!result.IsOk)
        {
            ErrorRaised?.Invoke(result.Message ?? "Delete failed.");
            return;
        }

        if (StillIn(from))
            RemoveFromListing(result.Data);
        _undo.Push(new(UndoAction.RestoreFromTrash, $"delete of {Describe(paths.Length)}",
            [.. paths.Select(p => new PathPair(p, string.Empty))]));
        InfoRaised?.Invoke($"Sent {Describe(targets.Count)} to the {TrashService.DisplayName}.");
    }

    private void DeleteItemsPermanent()
    {
        if (RefusedInArchive("Delete"))
            return;
        List<ListViewItem> targets = Targets();
        if (targets.Count == 0)
            return;

        string[] paths = [.. targets.Select(t => t.Item.FullPath)];
        string what = targets.Count == 1 ? targets[0].Name : $"{targets.Count} items";
        string from = DirectoryListing.CurrentDir;
        ConfirmRequested?.Invoke($"Permanently delete {what}? This cannot be undone.",
            () => _ = DeleteItemsPermanentAsync(paths, targets.Count, from));
    }

    private async Task DeleteItemsPermanentAsync(string[] paths, int count, string from)
    {
        ClearMarks();
        CommandResult<OpResult[]>? outcome = await _operation.RunAsync(
            $"Deleting {Describe(count)}",
            indeterminate: false,
            (progress, ct) => _core.Actions.DeleteItemsPermanentAsync(new(paths), progress, ct));

        if (outcome is not { } result)
            return;

        if (StillIn(from))
            RemoveFromListing(result.Data);
        int deleted = result.Data?.Count(r => r.Ok) ?? 0;
        if (!result.IsOk)
            ErrorRaised?.Invoke(DescribeFailure(result, "Delete"));
        else
            InfoRaised?.Invoke($"Permanently deleted {Describe(deleted)}.");
    }

    private void RemoveFromListing(OpResult[]? results)
    {
        if (results is null)
            return;
        foreach (OpResult op in results.Where(r => r.Ok))
            DirectoryListing.Remove(op.Path);
        DirectoryListing.ApplyView();
    }

    // ── the trash ────────────────────────────────────────────────────────
    // On Linux the trash is a folder like any other, so "go to the trash"
    // walks into it and everything else — opening, deleting for good — works
    // there as it does anywhere. Putting something back reads the record the
    // trash keeps of where it came from, so it goes home rather than here.

    private void GoToTrash()
    {
        CommandResult<string?> result = _core.Actions.OpenTrash(new());
        if (!result.IsOk)
        {
            ErrorRaised?.Invoke(result.Message ?? $"Could not open the {TrashService.DisplayName}.");
            return;
        }
        if (result.Data is { Length: > 0 } path)
            _ = SetCurrentDirectoryAsync(path);
        else
            InfoRaised?.Invoke($"Opened the {TrashService.DisplayName}.");
    }

    private void RestoreTrashedItems() => _ = RestoreTrashedItemsAsync();

    private async Task RestoreTrashedItemsAsync()
    {
        List<ListViewItem> targets = Targets();
        if (targets.Count == 0)
            return;

        string[] paths = [.. targets.Select(t => t.Item.FullPath)];
        string from = DirectoryListing.CurrentDir;
        ClearMarks();

        CommandResult<OpResult[]>? outcome = await _operation.RunAsync(
            $"Putting back {Describe(paths.Length)}",
            indeterminate: true,
            (progress, ct) => _core.Actions.RestoreTrashedItemsAsync(new(paths), progress, ct));

        if (outcome is not { } result)
            return;

        List<PathPair> undone = [];
        foreach (OpResult op in (result.Data ?? []).Where(o => o.Ok && o.Item is not null))
            undone.Add(new PathPair(op.Path, op.Item!.FullPath));

        if (StillIn(from))
            RemoveFromListing(result.Data);
        _undo.Push(new(UndoAction.RemoveCopies, $"putting back of {Describe(undone.Count)}", undone));

        if (!result.IsOk)
            ErrorRaised?.Invoke(DescribeFailure(result, "Put back"));
        else
            InfoRaised?.Invoke($"Put {Describe(undone.Count)} back.");
    }

    // ── clipboard verbs ──────────────────────────────────────────────────

    private void CopyItems()
    {
        if (!RefusedInArchive("Copy"))
            SetClipboard(ClipboardOp.Copy);
    }

    private void CutItems()
    {
        if (!RefusedInArchive("Cut"))
            SetClipboard(ClipboardOp.Cut);
    }

    private void SetClipboard(ClipboardOp op)
    {
        List<ListViewItem> targets = Targets();
        if (targets.Count == 0)
            return;
        string[] paths = [.. targets.Select(t => t.Item.FullPath)];
        _clipboard.Set(op, paths);
        _ = _systemClipboard.SetFilesAsync(paths, op);
        ClearMarks();
        string verb = op == ClipboardOp.Copy ? "Copied" : "Cut";
        InfoRaised?.Invoke($"{verb} {targets.Count} item{Plural(targets.Count)}.");
    }

    private void PasteItems()
    {
        if (!RefusedInArchive("Paste"))
            _ = PasteItemsAsync();
    }

    private async Task PasteItemsAsync()
    {
        string[] paths;
        ClipboardOp clipboardOp;
        (IReadOnlyList<string> Paths, ClipboardOp Op)? external = await _systemClipboard.TryGetFilesAsync();
        bool internalStillOnSystemClipboard = _clipboard.HasItems
            && external is { } current
            && SamePaths(current.Paths, _clipboard.Paths);

        if (internalStillOnSystemClipboard)
        {
            paths = [.. _clipboard.Paths];
            clipboardOp = _clipboard.Op;
        }
        else if (external is { } ext)
        {
            paths = [.. ext.Paths];
            clipboardOp = ext.Op;
            _clipboard.Clear();
        }
        else if (_clipboard.HasItems)
        {
            paths = [.. _clipboard.Paths];
            clipboardOp = _clipboard.Op;
        }
        else
        {
            InfoRaised?.Invoke("Nothing to paste.");
            return;
        }

        string target = DirectoryListing.CurrentDir;
        bool copying = clipboardOp == ClipboardOp.Copy;
        string verb = copying ? "Copying" : "Moving";

        CommandResult<OpResult[]>? outcome = await _operation.RunAsync(
            $"{verb} {Describe(paths.Length)}",
            indeterminate: false,
            (progress, ct) => copying
                ? _core.Actions.CopyItemsAsync(new(paths, target, Overwrite: false), progress, ct)
                : _core.Actions.MoveItemsAsync(new(paths, target, Overwrite: false), progress, ct));

        if (outcome is not { } result)
            return;

        int succeeded = 0;
        bool sameFolder = StillIn(target);
        List<PathPair> undone = [];
        if (result.Data is { } ops)
        {
            foreach (OpResult op in ops.Where(o => o.Ok))
            {
                succeeded++;
                if (op.Item is not null)
                    undone.Add(new PathPair(op.Path, op.Item.FullPath));
                // The user may have walked somewhere else while this ran; the
                // rows belong to the folder that was pasted into, not this one.
                if (op.Item is not null && sameFolder)
                    DirectoryListing.Upsert(op.Item);
            }
        }

        _undo.Push(new(
            copying ? UndoAction.RemoveCopies : UndoAction.MoveBack,
            $"paste of {Describe(undone.Count)}",
            undone));

        if (!copying)
            _clipboard.Clear();

        if (!result.IsOk)
            ErrorRaised?.Invoke(DescribeFailure(result, "Paste"));
        else
            InfoRaised?.Invoke($"Pasted {Describe(succeeded)}.");
    }

    private static bool SamePaths(IReadOnlyList<string> a, IReadOnlyList<string> b)
    {
        if (a.Count != b.Count)
            return false;
        HashSet<string> set = new(a, StringComparer.OrdinalIgnoreCase);
        return set.SetEquals(b);
    }

    /// <summary>Dim items sitting in the cut clipboard so "cut" is visible state.</summary>
    private void RefreshCutFlags()
    {
        bool isCut = _clipboard.HasItems && _clipboard.Op == ClipboardOp.Cut;
        HashSet<string> cutPaths = isCut
            ? new(_clipboard.Paths, StringComparer.OrdinalIgnoreCase)
            : [];
        foreach (ListViewItem item in DirectoryListing.Items)
            item.IsCut = isCut && cutPaths.Contains(item.Item.FullPath);
    }

    private void CopyPath()
    {
        List<ListViewItem> targets = Targets();
        if (targets.Count == 0)
        {
            // No highlight — copy the directory itself.
            _ = _systemClipboard.CopyTextAsync(DirectoryListing.CurrentDir);
            InfoRaised?.Invoke("Copied folder path.");
            return;
        }
        string text = string.Join(Environment.NewLine, targets.Select(t => t.Item.FullPath));
        _ = _systemClipboard.CopyTextAsync(text);
        InfoRaised?.Invoke($"Copied {targets.Count} path{Plural(targets.Count)}.");
    }

    // ── extract ──────────────────────────────────────────────────────────

    private void ExtractItems()
    {
        if (!RefusedInArchive("Extract"))
            _ = ExtractItemsAsync();
    }

    private async Task ExtractItemsAsync()
    {
        string[] paths = [.. Targets()
            .Where(t => !t.Item.IsDirectory && ArchiveService.IsArchive(t.Item.FullPath))
            .Select(t => t.Item.FullPath)];

        if (paths.Length == 0)
        {
            InfoRaised?.Invoke("Nothing to extract — highlight or mark a .zip file.");
            return;
        }

        string target = DirectoryListing.CurrentDir;
        ClearMarks();

        CommandResult<OpResult[]>? outcome = await _operation.RunAsync(
            $"Extracting {Describe(paths.Length)}",
            indeterminate: false,
            (progress, ct) => _core.Actions.ExtractArchivesAsync(new(paths, target), progress, ct));

        if (outcome is not { } result)
            return;

        int succeeded = 0;
        bool sameFolder = StillIn(target);
        List<PathPair> undone = [];
        foreach (OpResult op in (result.Data ?? []).Where(o => o.Ok && o.Item is not null))
        {
            succeeded++;
            undone.Add(new PathPair(op.Path, op.Item!.FullPath));
            if (sameFolder)
                DirectoryListing.Upsert(op.Item);
        }

        _undo.Push(new(UndoAction.RemoveCopies, $"extract of {Describe(undone.Count)}", undone));

        if (!result.IsOk)
            ErrorRaised?.Invoke(DescribeFailure(result, "Extract"));
        else
            InfoRaised?.Invoke($"Extracted {Describe(succeeded)}.");
    }

    // ── bookmarks ────────────────────────────────────────────────────────

    /// <summary>
    /// Remembers the highlighted item, or forgets it when it is already
    /// remembered. With nothing highlighted — an empty folder — the folder
    /// itself is what gets remembered, which is what a person standing in an
    /// empty folder means by "bookmark this".
    /// </summary>
    private void ToggleBookmark()
    {
        Bookmark mark = HighlightedItem is { } highlighted
            ? new(highlighted.Item.FullPath, highlighted.Item.Name, highlighted.Item.IsDirectory)
            : new(DirectoryListing.CurrentDir, FolderName(DirectoryListing.CurrentDir), true);

        bool added = _bookmarks.Toggle(mark);
        InfoRaised?.Invoke(added
            ? $"Bookmarked {mark.Name}{Shortcut(mark)}."
            : $"Removed the bookmark for {mark.Name}.");
    }

    /// <summary>" · Ctrl+3", or nothing past the ninth.</summary>
    private string Shortcut(Bookmark mark)
    {
        string key = BookmarkStore.ShortcutFor(_bookmarks.IndexOf(mark.Path));
        return key.Length == 0 ? string.Empty : $" · {key}";
    }

    private static string FolderName(string path)
    {
        string name = Path.GetFileName(Path.TrimEndingDirectorySeparator(LongPath.Display(path)));
        return name.Length > 0 ? name : LongPath.Display(path);
    }

    /// <summary>
    /// Goes where a bookmark points: into a folder, or to the folder holding
    /// a file with that file under the highlight. Landing next to a file is
    /// the useful thing — a bookmark is a place, not a thing to run.
    /// </summary>
    public void GoToBookmark(Bookmark mark)
    {
        if (mark.IsDirectory)
        {
            _ = SetCurrentDirectoryAsync(mark.Path);
            return;
        }

        string? parent = Path.GetDirectoryName(LongPath.Display(mark.Path));
        if (parent is not { Length: > 0 })
        {
            ErrorRaised?.Invoke($"{mark.Name} is not somewhere Rove can go.");
            return;
        }
        _ = SetCurrentDirectoryAsync(parent, mark.Path);
    }

    private void GoToBookmarkAt(int index)
    {
        if (_bookmarks.At(index) is not { } mark)
        {
            InfoRaised?.Invoke($"There is no bookmark {index + 1} yet.");
            return;
        }
        GoToBookmark(mark);
    }

    // ── compress ─────────────────────────────────────────────────────────

    private void CompressItems()
    {
        if (!RefusedInArchive("Compress"))
            _ = CompressItemsAsync();
    }

    private async Task CompressItemsAsync()
    {
        string[] paths = [.. Targets().Select(t => t.Item.FullPath)];
        if (paths.Length == 0)
        {
            InfoRaised?.Invoke("Nothing to compress — highlight or mark something first.");
            return;
        }

        string target = DirectoryListing.CurrentDir;
        ClearMarks();

        CommandResult<OpResult[]>? outcome = await _operation.RunAsync(
            $"Compressing {Describe(paths.Length)}",
            indeterminate: false,
            (progress, ct) => _core.Actions.CompressItemsAsync(new(paths, target), progress, ct));

        if (outcome is not { } result)
            return;

        if (!result.IsOk)
        {
            ErrorRaised?.Invoke(DescribeFailure(result, "Compress"));
            return;
        }

        OpResult made = (result.Data ?? [])[0];
        // Not RemoveCreated: that one only takes back something still empty,
        // and a zip that worked is the opposite of empty.
        _undo.Push(new(UndoAction.RemoveCopies, $"zip {Path.GetFileName(made.Path)}",
            [new PathPair(string.Empty, made.Path)]));

        if (StillIn(target) && made.Item is { } item)
        {
            DirectoryListing.Upsert(item);
            DirectoryListing.ListSelection.SelectPath(item.FullPath);
        }
        InfoRaised?.Invoke($"Compressed {Describe(paths.Length)} into {Path.GetFileName(made.Path)}.");
    }

    // ── undo ─────────────────────────────────────────────────────────────
    // Every finished action leaves behind what it would take to reverse it,
    // and undo runs that backwards. A step is used once: it comes off the
    // stack whether or not the reversal worked, so pressing undo twice walks
    // back two actions rather than fighting the same one.

    private void UndoLastAction() => _ = UndoLastActionAsync();

    private async Task UndoLastActionAsync()
    {
        if (_undo.Pop() is not { } step)
        {
            InfoRaised?.Invoke("Nothing to undo.");
            return;
        }

        switch (step.Action)
        {
            case UndoAction.RenameBack:
                UndoRename(step);
                break;
            case UndoAction.RemoveCreated:
                UndoCreate(step);
                break;
            case UndoAction.RemoveCopies:
            case UndoAction.MoveBack:
            case UndoAction.RestoreFromTrash:
                await UndoFileOperationAsync(step);
                break;
        }
    }

    private void UndoRename(UndoStep step)
    {
        PathPair pair = step.Items[0];
        string name = Path.GetFileName(Path.TrimEndingDirectorySeparator(pair.Before));

        CommandResult<FolderItem?> result = _core.Actions.RenameItem(new(pair.After, name));
        if (!result.IsOk || result.Data is null)
        {
            ErrorRaised?.Invoke(result.Message ?? $"Could not undo the {step.Description}.");
            return;
        }

        // Only touch the list when it is still showing the folder the item
        // lives in — otherwise the row belongs to somewhere else entirely.
        if (StillIn(Path.GetDirectoryName(pair.After) ?? string.Empty))
        {
            DirectoryListing.Rename(pair.After, result.Data);
            DirectoryListing.ListSelection.SelectPath(result.Data.FullPath);
        }
        InfoRaised?.Invoke($"Undid the {step.Description}.");
    }

    private void UndoCreate(UndoStep step)
    {
        string path = step.Items[0].After;
        CommandResult<string?> result = _core.Actions.DeleteIfEmpty(new(path));

        // Already gone is the state undo was aiming for.
        if (!result.IsOk && result.Reason != "not_found")
        {
            ErrorRaised?.Invoke(result.Message ?? $"Could not undo the {step.Description}.");
            return;
        }

        DirectoryListing.Remove(path);
        DirectoryListing.ApplyView();
        InfoRaised?.Invoke($"Undid the {step.Description}.");
    }

    private async Task UndoFileOperationAsync(UndoStep step)
    {
        string here = DirectoryListing.CurrentDir;
        CommandResult<OpResult[]>? outcome = await _operation.RunAsync(
            $"Undoing the {step.Description}",
            indeterminate: step.Action == UndoAction.RestoreFromTrash,
            (progress, ct) => step.Action switch
            {
                UndoAction.RemoveCopies => _core.Actions.RemoveItemsAsync(
                    new([.. step.Items.Select(i => i.After)]), progress, ct),
                UndoAction.RestoreFromTrash => _core.Actions.RestoreItemsAsync(
                    new([.. step.Items.Select(i => i.Before)]), progress, ct),
                _ => MoveBackAsync(step, progress, ct),
            });

        if (outcome is not { } result)
            return;

        // The listing has holes in it either way now — rows that went away and
        // rows that came back — so re-read the folder rather than patching it.
        if (StillIn(here))
            await ReloadCurrentDirectoryAsync();

        if (!result.IsOk)
            ErrorRaised?.Invoke(DescribeFailure(result, "Undo"));
        else
            InfoRaised?.Invoke($"Undid the {step.Description}.");
    }

    /// <summary>
    /// Puts moved items back where each one came from — a paste can pull from
    /// more than one folder, so this runs one move per source folder.
    /// </summary>
    private async Task<CommandResult<OpResult[]>> MoveBackAsync(
        UndoStep step, IProgress<FileOpProgress> progress, CancellationToken ct
    )
    {
        List<OpResult> all = [];
        IEnumerable<IGrouping<string, PathPair>> byFolder = step.Items
            .GroupBy(i => Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(i.Before)) ?? string.Empty,
                PathCompare.Comparer);

        foreach (IGrouping<string, PathPair> folder in byFolder)
        {
            if (folder.Key.Length == 0)
                continue;
            CommandResult<OpResult[]> result = await _core.Actions.MoveItemsAsync(
                new([.. folder.Select(i => i.After)], folder.Key, Overwrite: false), progress, ct);
            all.AddRange(result.Data ?? []);
        }

        int failed = all.Count(r => !r.Ok);
        if (failed == 0)
            return CommandResult<OpResult[]>.Ok([.. all]);

        string message = string.Join("; ", all.Where(r => !r.Ok).Select(r => r.Message ?? r.Reason).Distinct());
        string reason = failed == all.Count ? all.First(r => !r.Ok).Reason : "partial_failure";
        return CommandResult<OpResult[]>.Fail(reason, message, [.. all]);
    }

    // ── create ───────────────────────────────────────────────────────────

    private void ToggleCreateFile() => ToggleCreate(isFolder: false);
    private void ToggleCreateFolder() => ToggleCreate(isFolder: true);

    private void ToggleCreate(bool isFolder)
    {
        if (!InCreateItem && RefusedInArchive(isFolder ? "New folder" : "New file"))
            return;
        CreateItemText = string.Empty;
        _createIsFolder = isFolder;
        CreateItemLabel = isFolder ? "New folder" : "New file";
        InCreateItem = !InCreateItem;
    }

    private void CancelCreate() => InCreateItem = false;

    private void ApplyCreate()
    {
        string name = CreateItemText.Trim();
        if (name.Length == 0)
        {
            InCreateItem = false;
            return;
        }

        CommandResult<FolderItem?> result = _core.Actions.CreateItem(
            new(DirectoryListing.CurrentDir, name, IsDirectory: _createIsFolder)
        );
        if (!result.IsOk || result.Data is null)
        {
            // Keep the input open so the name can be fixed.
            ErrorRaised?.Invoke(result.Message ?? "Create failed.");
            return;
        }

        InCreateItem = false;
        DirectoryListing.Upsert(result.Data);
        DirectoryListing.ListSelection.SelectPath(result.Data.FullPath);
        _undo.Push(new(UndoAction.RemoveCreated, $"new {(_createIsFolder ? "folder" : "file")} {name}",
            [new PathPair(string.Empty, result.Data.FullPath)]));
    }

    // ── plumbing ─────────────────────────────────────────────────────────

    private static string Plural(int n) => n == 1 ? "" : "s";

    private static string Describe(int n) => $"{n} item{Plural(n)}";

    /// <summary>
    /// True when the list is still showing the folder an operation started
    /// against. Operations run in the background now, so by the time one
    /// finishes the user may be looking at something else entirely.
    /// </summary>
    private bool StillIn(string directory) => PathCompare.PathMatches(DirectoryListing.CurrentDir, directory);

    /// <summary>
    /// A cancelled run is not a failure to shout about — say what got done and
    /// leave it there. Anything else keeps the backend's own message.
    /// </summary>
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
        _registry.Register(CommandDef.PathCompleteAccept, AcceptCompletion);
        _registry.Register(CommandDef.PathCompleteDismiss, DismissCompletions);
        _registry.Register(CommandDef.ShowDrives, ShowDrives);
        _registry.Register(CommandDef.ShowTrash, GoToTrash);
        _registry.Register(CommandDef.RestoreTrashedItems, RestoreTrashedItems);
        _registry.Register(CommandDef.ToggleLocalSearch, ToggleLocalSearch);
        _registry.Register(CommandDef.ApplyLocalSearch, LeaveLocalSearchTyping);
        _registry.Register(CommandDef.ToggleRenameItem, ToggleRenameItem);
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
        _registry.Register(CommandDef.SortByName, () => DirectoryListing.SetSort(SortKey.Name));
        _registry.Register(CommandDef.SortByType, () => DirectoryListing.SetSort(SortKey.Type));
        _registry.Register(CommandDef.SortBySize, () => DirectoryListing.SetSort(SortKey.Size));
        _registry.Register(CommandDef.SortByModified, () => DirectoryListing.SetSort(SortKey.Modified));
        _registry.Register(CommandDef.ToggleContentView, CycleContentView);
        _registry.Register(CommandDef.ToggleMarkItem, ToggleMarkItem);
        _registry.Register(CommandDef.ClearMarks, ClearMarks);
        _registry.Register(CommandDef.EscapeBrowse, EscapeBrowse);
        _registry.Register(CommandDef.ToggleShowHidden, ToggleShowHidden);
        _registry.Register(CommandDef.DeleteItems, DeleteItems);
        _registry.Register(CommandDef.DeleteItemsPermanent, DeleteItemsPermanent);
        _registry.Register(CommandDef.CopyItems, CopyItems);
        _registry.Register(CommandDef.CutItems, CutItems);
        _registry.Register(CommandDef.PasteItems, PasteItems);
        _registry.Register(CommandDef.CopyPath, CopyPath);
        _registry.Register(CommandDef.ExtractArchives, ExtractItems);
        _registry.Register(CommandDef.CompressItems, CompressItems);
        _registry.Register(CommandDef.ToggleBookmark, ToggleBookmark);

        // One command per shortcut slot, not per bookmark: the slots are
        // fixed, and which bookmark a slot leads to is a question asked when
        // the key is pressed.
        for (int slot = 0; slot < BookmarkStore.ShortcutCount; slot++)
        {
            int index = slot;
            _registry.Register(CommandDef.BookmarkGo(index), () => GoToBookmarkAt(index));
        }
        _registry.Register(CommandDef.UndoLastAction, UndoLastAction);
        _registry.Register(CommandDef.ToggleCreateFile, ToggleCreateFile);
        _registry.Register(CommandDef.ToggleCreateFolder, ToggleCreateFolder);
        _registry.Register(CommandDef.ApplyCreate, ApplyCreate);
        _registry.Register(CommandDef.CancelCreate, CancelCreate);
        _registry.Register(CommandDef.CancelFileOperation, _operation.Cancel);
    }
}
