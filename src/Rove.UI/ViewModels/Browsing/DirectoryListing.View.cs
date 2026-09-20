using Rove.Core.Services;
using System.IO.Enumeration;

namespace Rove.UI.ViewModels;

public partial class DirectoryListing
{
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
}
