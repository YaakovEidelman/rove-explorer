using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Rove.Core.Protocol;
using Rove.Core.Services;
using Rove.UI.Models;
using Rove.UI.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Linq;

namespace Rove.UI.ViewModels;

public partial class DirectoryListing : ObservableObject
{
    private readonly IIconCache _cache;

    /// <summary>
    /// Below this many rows the whole list is cheap to redo, so a filter runs
    /// on the keystroke; above it the typing is given a moment to settle
    /// first.
    /// </summary>
    private const int InstantFilterLimit = 2_000;

    /// <summary>How long typing has to pause before a big folder is refiltered.</summary>
    private static readonly TimeSpan _filterDelay = TimeSpan.FromMilliseconds(90);

    /// <summary>
    /// Past this much churn the visible list is replaced in one shot instead
    /// of row by row: a hundred separate notifications cost more than one
    /// rebuild.
    /// </summary>
    private const int BulkResetThreshold = 64;

    private readonly DispatcherTimer _filterTimer;
    private readonly SettingsStore? _settings;

    public DirectoryListing(IIconCache cache, SettingsStore? settings = null)
    {
        _cache = cache;
        _settings = settings;
        ListSelection = new(Items);
        _filterTimer = new DispatcherTimer { Interval = _filterDelay };
        _filterTimer.Tick += (_, _) => ApplyView();
    }

    private List<ListViewItem> _unfilteredContent = [];

    /// <summary>
    /// Glob patterns a picker caller restricted files to (the portal's
    /// `filters` option). Folders stay visible regardless — they're not
    /// what's being filtered, they're how you get to what is.
    /// </summary>
    private string[]? _selectionFilter;

    private int _iconSize = 16;

    public RowCollection Items { get; } = [];

    [ObservableProperty]
    private string _currentDir = PathCompare.DefaultStartDirectory();

    /// <summary>
    /// <see cref="CurrentDir"/> broken into the steps the top bar draws in
    /// their own boxes. Derived rather than stored, so the two can never come
    /// to disagree about where you are.
    /// </summary>
    public PathCrumb[] Crumbs => PathBreadcrumb.Of(CurrentDir,
        TrashService.BrowsePath is { } trashRoot ? (trashRoot, TrashService.DisplayName) : null);

    partial void OnCurrentDirChanged(string value) => OnPropertyChanged(nameof(Crumbs));

    [ObservableProperty]
    private bool _inLocalSearch;

    [ObservableProperty]
    private string _searchCurrentDirectoryText = string.Empty;

    [ObservableProperty]
    private bool _emptyDirectory;

    /// <summary>What the rows are ordered by, below the directories-first split.</summary>
    [ObservableProperty]
    private SortKey _sortBy = SortKey.Name;

    [ObservableProperty]
    private bool _sortDescending;

    /// <summary>The arrow a column header shows when it's the active sort key, else nothing.</summary>
    public string NameSortIndicator => IndicatorFor(SortKey.Name);
    public string TypeSortIndicator => IndicatorFor(SortKey.Type);
    public string SizeSortIndicator => IndicatorFor(SortKey.Size);
    public string ModifiedSortIndicator => IndicatorFor(SortKey.Modified);

    private string IndicatorFor(SortKey key) => SortBy != key ? "" : SortDescending ? "▼" : "▲";

    partial void OnSortByChanged(SortKey value) => NotifySortIndicators();

    partial void OnSortDescendingChanged(bool value) => NotifySortIndicators();

    private void NotifySortIndicators()
    {
        OnPropertyChanged(nameof(NameSortIndicator));
        OnPropertyChanged(nameof(TypeSortIndicator));
        OnPropertyChanged(nameof(SizeSortIndicator));
        OnPropertyChanged(nameof(ModifiedSortIndicator));
    }

    /// <summary>
    /// Whether items the OS marks hidden are in the list. Sticky: it is a way
    /// of looking at every folder, not a property of this one.
    /// </summary>
    [ObservableProperty]
    private bool _showHidden;

    /// <summary>The highlight over <see cref="Items"/>.</summary>
    public ListSelection ListSelection { get; }

    /// <summary>
    /// Items the OS calls its own — Windows marks them System — are never
    /// listed. Hidden is the user's to decide, so those are held and left out
    /// of the view instead (see <see cref="Shown"/>).
    /// </summary>
    private static bool IsKept(FolderItem item) => !item.Attributes.HasFlag(FileAttributes.System);

    private static bool IsHidden(FolderItem item) => item.Attributes.HasFlag(FileAttributes.Hidden);

    /// <summary>
    /// Restricts which files can be picked, by name glob (e.g. "*.png").
    /// Pass null or empty to lift the restriction.
    /// </summary>
    public void SetSelectionFilter(IReadOnlyList<string>? patterns)
    {
        _selectionFilter = patterns is { Count: > 0 } ? [.. patterns] : null;
        InvalidateFilter();
        ApplyView();
    }

    public void Load(string directory, FolderItem[] items)
    {
        if (!PathCompare.PathMatches(directory, CurrentDir))
            ApplyDefaultSort(directory);
        _unfilteredContent = [.. items.Where(IsKept).Select(i => new ListViewItem(i, _iconSize, _cache))];
        InvalidateFilter();
        SortContent();
        ApplyView();
    }

    /// <summary>
    /// The sort a folder starts on, before anyone has touched it this visit.
    /// Everywhere is name order except Downloads, which is more useful sorted
    /// by what just landed in it.
    /// </summary>
    private void ApplyDefaultSort(string directory)
    {
        bool sortDownloadsByTime = _settings?.Current.SortDownloadsByTime ?? true;
        bool isDownloads = sortDownloadsByTime && PathCompare.PathMatches(directory, RovePaths.DownloadsDirectory);
        SortBy = isDownloads ? SortKey.Modified : SortKey.Name;
        SortDescending = isDownloads;
    }

    /// <summary>
    /// Picks a new sort, or — asked for the one already active — flips its
    /// direction instead of doing nothing.
    /// </summary>
    public void SetSort(SortKey key)
    {
        if (SortBy == key)
        {
            SortDescending = !SortDescending;
        }
        else
        {
            SortBy = key;
            SortDescending = key is SortKey.Size or SortKey.Modified;
        }
        // The cached "shown"/"matched" snapshots hold the pre-sort order —
        // stale until rebuilt, same as after any other content change.
        InvalidateFilter();
        SortContent();
        ApplyView();
    }

    /// <summary>
    /// Every row still on screen re-fetches its icon at the new size; rows
    /// created afterwards (a paste, a watcher event) just start there.
    /// </summary>
    public void SetIconSize(int size)
    {
        if (_iconSize == size)
            return;
        _iconSize = size;
        foreach (ListViewItem item in _unfilteredContent)
            item.SetIconSize(size);
    }

    partial void OnShowHiddenChanged(bool value)
    {
        InvalidateFilter();
        ApplyView();
    }

    partial void OnSearchCurrentDirectoryTextChanged(string value)
    {
        if (_unfilteredContent.Count <= InstantFilterLimit)
            ApplyView();
        else
            ScheduleApplyView();
    }

    /// <summary>Waits for a gap in the typing, restarting the clock on each keystroke.</summary>
    private void ScheduleApplyView()
    {
        _filterTimer.Stop();
        _filterTimer.Start();
    }

    /// <summary>
    /// Runs a filter that is still waiting on the clock. Anything that acts on
    /// the highlighted row calls this first, so a fast typist can never open
    /// an item from a list that is one keystroke out of date.
    /// </summary>
    public void FlushPendingFilter()
    {
        if (_filterTimer.IsEnabled)
            ApplyView();
    }

    public void ApplyView()
    {
        _filterTimer.Stop();

        IReadOnlyList<ListViewItem> shown = Shown();
        // Filtering follows the query, not whether the box still has focus —
        // leaving the box (Enter) keeps the filtered view up until something
        // clears the query.
        IReadOnlyList<ListViewItem> target = SearchCurrentDirectoryText.Length > 0 ? Filtered(shown) : shown;

        // Take the highlight off the control first: it mirrors the highlight
        // two-way and would push a stale index back while rows come and go.
        ListSelection.Detach();
        SyncItems(target);
        EmptyDirectory = Items.Count == 0;
        ListSelection.Reconcile();
    }

    // ── filtering ────────────────────────────────────────────────────────

    private List<ListViewItem>? _lastMatches;
    private string _lastQuery = string.Empty;
    private List<ListViewItem>? _lastShown;

    /// <summary>
    /// The rows the current view lets through, before any filter text. Held
    /// until the folder or the toggle changes, so flipping between filtered
    /// and unfiltered costs nothing.
    /// </summary>
    private IReadOnlyList<ListViewItem> Shown()
    {
        IReadOnlyList<ListViewItem> shown = ShowHidden
            ? _unfilteredContent
            : _lastShown ??= [.. _unfilteredContent.Where(row => !IsHidden(row.Item))];
        return _selectionFilter is null ? shown : [.. shown.Where(MatchesSelectionFilter)];
    }

    private bool MatchesSelectionFilter(ListViewItem row) =>
        row.Item.IsDirectory
        || _selectionFilter!.Any(pattern => FileSystemName.MatchesSimpleExpression(pattern, row.Item.Name));

    /// <summary>
    /// Rows matching the filter box. Typing one more character can only ever
    /// shrink the previous result — every character of the query still has to
    /// appear, in order — so the next pass looks at what survived last time
    /// instead of the whole folder again.
    /// </summary>
    private List<ListViewItem> Filtered(IReadOnlyList<ListViewItem> shown)
    {
        string query = SearchCurrentDirectoryText;
        IReadOnlyList<ListViewItem> source =
            _lastMatches is not null && _lastQuery.Length > 0 && query.StartsWith(_lastQuery, StringComparison.Ordinal)
                ? _lastMatches
                : shown;

        List<ListViewItem> matches = new(source.Count);
        for (int i = 0; i < source.Count; i++)
        {
            if (FuzzyMatcher.TryMatch(query, source[i].Item.Name, out _))
                matches.Add(source[i]);
        }

        _lastMatches = matches;
        _lastQuery = query;
        return matches;
    }

    /// <summary>The folder changed underneath the filter — the cached result is stale.</summary>
    private void InvalidateFilter()
    {
        _lastMatches = null;
        _lastQuery = string.Empty;
        _lastShown = null;
    }

    // ── syncing the visible rows ─────────────────────────────────────────

    /// <summary>
    /// Brings the visible collection in line with the target list. The common
    /// case — nothing moved — walks each list once and touches nothing.
    /// </summary>
    private void SyncItems(IReadOnlyList<ListViewItem> target)
    {
        HashSet<ListViewItem> keep = [.. target];

        int shared = 0;
        for (int i = 0; i < Items.Count; i++)
        {
            if (keep.Contains(Items[i]))
                shared++;
        }

        int churn = Items.Count - shared + target.Count - shared;
        if (churn > BulkResetThreshold)
        {
            Items.ResetTo(target);
            return;
        }

        for (int i = Items.Count - 1; i >= 0; i--)
        {
            if (!keep.Contains(Items[i]))
                Items.RemoveAt(i);
        }

        for (int i = 0; i < target.Count; i++)
        {
            ListViewItem item = target[i];
            if (i < Items.Count && ReferenceEquals(Items[i], item))
                continue;

            // Everything before i already matches, so the row can only be
            // further down — no need to rescan from the top.
            int existing = IndexOfFrom(item, i);
            if (existing >= 0)
                Items.Move(existing, i);
            else
                Items.Insert(i, item);
        }
    }

    private int IndexOfFrom(ListViewItem item, int start)
    {
        for (int i = start; i < Items.Count; i++)
        {
            if (ReferenceEquals(Items[i], item))
                return i;
        }
        return -1;
    }

    // ── content changes ──────────────────────────────────────────────────

    private int GetIndexFromContent(string path) =>
        _unfilteredContent.FindIndex(x => PathCompare.PathMatches(x.Item.FullPath, path));

    /// <summary>
    /// Whether a path names something in the folder on screen. Watcher events
    /// can arrive from the folder that was being watched a moment ago — the
    /// watcher is pointed elsewhere, but what it already saw is still on its
    /// way — and a row for something that lives somewhere else does not
    /// belong in this list.
    /// </summary>
    private bool Belongs(string path) =>
        Path.GetDirectoryName(LongPath.Display(path)) is { Length: > 0 } parent
        && PathCompare.PathMatches(parent, CurrentDir);

    public void Upsert(FolderItem item)
    {
        if (!Belongs(item.FullPath))
            return;
        MutateUpsert(item);
        SortContent();
        ApplyView();
    }

    private void MutateUpsert(FolderItem item) => MutateUpsert(GetIndexFromContent(item.FullPath), item);

    private void MutateUpsert(int index, FolderItem item)
    {
        if (!IsKept(item))
        {
            RemoveAt(index);
            return;
        }
        if (index != -1)
        {
            ListViewItem row = _unfilteredContent[index];
            if (!string.Equals(row.Name, item.Name, StringComparison.Ordinal)
                || IsHidden(row.Item) != IsHidden(item))
            {
                InvalidateFilter();
            }
            row.UpdateData(item);
        }
        else
        {
            _unfilteredContent.Add(new(item, _iconSize, _cache));
            InvalidateFilter();
        }
    }

    public void Rename(string oldPath, FolderItem item)
    {
        if (!Belongs(item.FullPath))
        {
            RemoveWithApply(oldPath); // renamed out of this folder
            return;
        }
        MutateRename(oldPath, item);
        SortContent();
        ApplyView();
    }

    private void MutateRename(string oldPath, FolderItem item)
    {
        int existingIndex = GetIndexFromContent(oldPath);
        if (existingIndex == -1)
            existingIndex = GetIndexFromContent(item.FullPath);
        MutateUpsert(existingIndex, item);
        InvalidateFilter(); // the new name may match a filter the old one didn't
    }

    public void Remove(string path) => RemoveAt(GetIndexFromContent(path));

    public void RemoveWithApply(string path)
    {
        Remove(path);
        ApplyView();
    }

    private void RemoveAt(int index)
    {
        if (index == -1)
            return;
        _unfilteredContent.RemoveAt(index);
        InvalidateFilter();
    }

    /// <summary>
    /// A coalesced batch of watcher events, applied as one resort and one
    /// view rebuild instead of one apiece — the fix for a fast folder (an
    /// archive extracting into it, say) choking the list on every event.
    /// </summary>
    public void ApplyBatch(IReadOnlyList<WatchEvent> events)
    {
        foreach (WatchEvent e in events)
        {
            switch (e)
            {
                case WatchEvent.Upserted(FolderItem item):
                    if (Belongs(item.FullPath))
                        MutateUpsert(item);
                    break;
                case WatchEvent.Deleted(string path):
                    Remove(path);
                    break;
                case WatchEvent.Renamed(string oldPath, FolderItem item):
                    if (Belongs(item.FullPath))
                        MutateRename(oldPath, item);
                    else
                        Remove(oldPath);
                    break;
            }
        }
        SortContent();
        ApplyView();
    }

    /// <summary>Directories first, then by <see cref="SortBy"/> — ties broken by name.</summary>
    private void SortContent()
    {
        Comparison<ListViewItem> byKey = SortBy switch
        {
            SortKey.Type => static (a, b) => string.Compare(a.TypeText, b.TypeText, StringComparison.OrdinalIgnoreCase),
            SortKey.Size => static (a, b) => (a.Item.Size ?? -1).CompareTo(b.Item.Size ?? -1),
            SortKey.Modified => static (a, b) => a.Item.LastWriteTime.CompareTo(b.Item.LastWriteTime),
            _ => static (a, b) => string.Compare(a.Item.Name, b.Item.Name, StringComparison.OrdinalIgnoreCase),
        };
        bool descending = SortDescending;

        _unfilteredContent.Sort((a, b) =>
        {
            if (a.Item.IsDirectory != b.Item.IsDirectory)
                return a.Item.IsDirectory ? -1 : 1;
            int primary = byKey(a, b);
            if (descending)
                primary = -primary;
            return primary != 0 ? primary : string.Compare(a.Item.Name, b.Item.Name, StringComparison.OrdinalIgnoreCase);
        });
    }
}
