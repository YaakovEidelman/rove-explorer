using CommunityToolkit.Mvvm.ComponentModel;
using Rove.UI.Services;
using System.Collections.ObjectModel;
using Rove.UI.Models;

namespace Rove.UI.ViewModels;

public partial class ContentViewModel
{
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

    partial void OnViewModeChanged(ContentViewMode value)
    {
        ApplyIconSize();
        if (IsIconView && InResizeColumns)
            ToggleResizeColumns();
    }
    partial void OnIconSizeChanged(IconSize value) => ApplyIconSize();

    private void ApplyIconSize() =>
        DirectoryListing.SetIconSize(ViewMode == ContentViewMode.List ? ListIconPixels : IconSizes.PixelsFor(IconSize));

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
    private bool _inResizeColumns;

    private int _activeColumnIndex;

    public FolderViewColumn? ActiveColumn =>
        _activeColumnIndex >= 0 && _activeColumnIndex < Columns.Count ? Columns[_activeColumnIndex] : null;

    public void ContentMoveUp() => DirectoryListing.ListSelection.MoveUp();
    public void ContentMoveDown() => DirectoryListing.ListSelection.MoveDown();
    public void ContentMoveTop() => DirectoryListing.ListSelection.MoveTop();
    public void ContentMoveBottom() => DirectoryListing.ListSelection.MoveBottom();

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

    private void ToggleLocalSearch()
    {
        DirectoryListing.FlushPendingFilter();
        DirectoryListing.SearchCurrentDirectoryText = string.Empty;
        DirectoryListing.InLocalSearch = !DirectoryListing.InLocalSearch;
        DirectoryListing.ApplyView();
    }

    private void LeaveLocalSearchTyping()
    {
        DirectoryListing.FlushPendingFilter();
        DirectoryListing.InLocalSearch = false;
    }

    private void ClearLocalSearch()
    {
        if (!DirectoryListing.InLocalSearch && DirectoryListing.SearchCurrentDirectoryText.Length == 0)
            return;
        DirectoryListing.InLocalSearch = false;
        DirectoryListing.SearchCurrentDirectoryText = string.Empty;
        DirectoryListing.ApplyView();
    }

    private void EscapeBrowse()
    {
        ClearMarks();
        ClearLocalSearch();
    }

    private const double WidthStep = 12;
    private const double WidthStepLarge = 48;

    private bool RefusedInIconView(string verb)
    {
        if (!IsIconView)
            return false;
        InfoRaised?.Invoke($"{verb} is only for the list view.");
        return true;
    }

    private void SortBy(SortKey key)
    {
        if (RefusedInIconView("Sorting"))
            return;
        DirectoryListing.SetSort(key);
    }

    private void ToggleResizeColumns()
    {
        if (!InResizeColumns && RefusedInIconView("Resizing columns"))
            return;
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

    private static readonly SortKey[] _columnSortKeys = [SortKey.Name, SortKey.Type, SortKey.Size, SortKey.Modified];

    private void SortByActiveColumn()
    {
        if (_activeColumnIndex < 0 || _activeColumnIndex >= _columnSortKeys.Length)
            return;
        SortBy(_columnSortKeys[_activeColumnIndex]);
    }

    private void ColumnNext() => StepActiveColumn(1);
    private void ColumnPrev() => StepActiveColumn(-1);

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

    private void ToggleShowHidden()
    {
        DirectoryListing.ShowHidden = !DirectoryListing.ShowHidden;
        InfoRaised?.Invoke(DirectoryListing.ShowHidden
            ? "Showing hidden items."
            : "Hiding hidden items.");
    }

    private void ToggleGroupByDate()
    {
        DirectoryListing.GroupByDate = !DirectoryListing.GroupByDate;
        InfoRaised?.Invoke(DirectoryListing.GroupByDate
            ? "Grouping by date when sorted by date modified."
            : "Not grouping by date.");
    }

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
}
