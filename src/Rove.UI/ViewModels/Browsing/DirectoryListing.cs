using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Rove.Core.Protocol;
using Rove.Core.Services;
using Rove.UI.Models;
using Rove.UI.Services;

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

    [ObservableProperty]
    private bool _groupByDate;

    partial void OnGroupByDateChanged(bool value) => ApplyView();

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
        bool foldersFirst = SortBy != SortKey.Modified;

        _unfilteredContent.Sort((a, b) =>
        {
            if (foldersFirst && a.Item.IsDirectory != b.Item.IsDirectory)
                return a.Item.IsDirectory ? -1 : 1;
            int primary = byKey(a, b);
            if (descending)
                primary = -primary;
            return primary != 0 ? primary : string.Compare(a.Item.Name, b.Item.Name, StringComparison.OrdinalIgnoreCase);
        });
    }
}
