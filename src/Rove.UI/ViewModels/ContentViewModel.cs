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
    private bool _isLoading;

    /// <summary>
    /// Whether what is listed is the inside of a zip rather than a folder on
    /// disk. Worked out once per move, when the path is already in hand,
    /// because answering it means asking the filesystem whether a step of
    /// the path is really a file.
    /// </summary>
    [ObservableProperty]
    private bool _inArchive;

    /// <summary>
    /// Whether what is listed is the trash (or somewhere nested inside a
    /// trashed folder), worked out the same way and at the same point as
    /// <see cref="InArchive"/>.
    /// </summary>
    [ObservableProperty]
    private bool _inTrash;

    [ObservableProperty]
    private bool _isAdminView;

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
    /// Verbs that don't make sense on something already in the trash — most
    /// of what a folder view normally allows, since the trash is somewhere
    /// to look at and put back from, not somewhere to keep working.
    /// </summary>
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

    /// <summary>
    /// Where a deep search starts. Inside a zip there is no tree on disk to
    /// walk, so the search runs from the folder the archive itself sits in.
    /// </summary>
    public string SearchRoot =>
        ArchivePath.TryParse(DirectoryListing.CurrentDir, out ArchivePath inside)
            ? Path.GetDirectoryName(LongPath.Display(inside.Archive)) ?? DirectoryListing.CurrentDir
            : DirectoryListing.CurrentDir;

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
        _registry.Register(CommandDef.RestoreAllTrashedItems, RestoreAllTrashedItems);
        _registry.Register(CommandDef.EmptyTrash, EmptyTrash);
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
        _registry.Register(CommandDef.SortByName, () => SortBy(SortKey.Name));
        _registry.Register(CommandDef.SortByType, () => SortBy(SortKey.Type));
        _registry.Register(CommandDef.SortBySize, () => SortBy(SortKey.Size));
        _registry.Register(CommandDef.SortByModified, () => SortBy(SortKey.Modified));
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
