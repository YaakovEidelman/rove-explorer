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
    private bool _inResizeColumns;

    private int _activeColumnIndex;

    /// <summary>The column the width keys act on while the resize bar is up.</summary>
    public FolderViewColumn? ActiveColumn =>
        _activeColumnIndex >= 0 && _activeColumnIndex < Columns.Count ? Columns[_activeColumnIndex] : null;

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

    // ── column widths ────────────────────────────────────────────────────
    // Widths are keyboard-only: the bar at the bottom is the visible surface,
    // and the highlighted header says which column the keys are pointed at.

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

    /// <summary>Same order as <see cref="ColumnDefaults.Create"/> — one sort key per column.</summary>
    private static readonly SortKey[] _columnSortKeys = [SortKey.Name, SortKey.Type, SortKey.Size, SortKey.Modified];

    private void SortByActiveColumn()
    {
        if (_activeColumnIndex < 0 || _activeColumnIndex >= _columnSortKeys.Length)
            return;
        SortBy(_columnSortKeys[_activeColumnIndex]);
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
}
