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

    private const int InstantFilterLimit = 2_000;

    private static readonly TimeSpan _filterDelay = TimeSpan.FromMilliseconds(90);

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

    private string[]? _selectionFilter;

    private int _iconSize = 16;

    public RowCollection Items { get; } = [];

    [ObservableProperty]
    private string _currentDir = PathCompare.DefaultStartDirectory();

    public PathCrumb[] Crumbs => PathBreadcrumb.Of(CurrentDir,
        TrashService.BrowsePath is { } trashRoot ? (trashRoot, TrashService.DisplayName) : null);

    partial void OnCurrentDirChanged(string value) => OnPropertyChanged(nameof(Crumbs));

    [ObservableProperty]
    private bool _inLocalSearch;

    [ObservableProperty]
    private string _searchCurrentDirectoryText = string.Empty;

    [ObservableProperty]
    private bool _emptyDirectory;

    [ObservableProperty]
    private SortKey _sortBy = SortKey.Name;

    [ObservableProperty]
    private bool _sortDescending;

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

    [ObservableProperty]
    private bool _showHidden;

    public ListSelection ListSelection { get; }

    private static bool IsKept(FolderItem item) => !item.Attributes.HasFlag(FileAttributes.System);

    private static bool IsHidden(FolderItem item) => item.Attributes.HasFlag(FileAttributes.Hidden);

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

    private void ApplyDefaultSort(string directory)
    {
        bool sortDownloadsByTime = _settings?.Current.SortDownloadsByTime ?? true;
        bool isDownloads = sortDownloadsByTime && PathCompare.PathMatches(directory, RovePaths.DownloadsDirectory);
        SortBy = isDownloads ? SortKey.Modified : SortKey.Name;
        SortDescending = isDownloads;
    }

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
        InvalidateFilter();
        SortContent();
        ApplyView();
    }

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

    private void ScheduleApplyView()
    {
        _filterTimer.Stop();
        _filterTimer.Start();
    }

    public void FlushPendingFilter()
    {
        if (_filterTimer.IsEnabled)
            ApplyView();
    }

    public void ApplyView()
    {
        _filterTimer.Stop();

        IReadOnlyList<ListViewItem> shown = Shown();
        IReadOnlyList<ListViewItem> target = SearchCurrentDirectoryText.Length > 0 ? Filtered(shown) : shown;

        ListSelection.Detach();
        SyncItems(target);
        EmptyDirectory = Items.Count == 0;
        ListSelection.Reconcile();
    }

    private List<ListViewItem>? _lastMatches;
    private string _lastQuery = string.Empty;
    private List<ListViewItem>? _lastShown;

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

    private void InvalidateFilter()
    {
        _lastMatches = null;
        _lastQuery = string.Empty;
        _lastShown = null;
    }

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

    private int GetIndexFromContent(string path) =>
        _unfilteredContent.FindIndex(x => PathCompare.PathMatches(x.Item.FullPath, path));

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
            RemoveWithApply(oldPath);
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
        InvalidateFilter();
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
