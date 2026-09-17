using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace Rove.UI.ViewModels;

/// <summary>
/// The visible rows. Same as an ordinary observable list, except it can be
/// replaced wholesale in one go: every listener (the list control, the status
/// bar) is told once instead of once per row, which is the difference between
/// opening a 50,000-item folder in a moment and in several seconds.
/// </summary>
public sealed class RowCollection : ObservableCollection<ListViewItem>
{
    private static readonly PropertyChangedEventArgs _countChanged = new(nameof(Count));
    private static readonly PropertyChangedEventArgs _indexerChanged = new("Item[]");

    public void ResetTo(IReadOnlyList<ListViewItem> rows)
    {
        Items.Clear();
        for (int i = 0; i < rows.Count; i++)
            Items.Add(rows[i]);

        OnPropertyChanged(_countChanged);
        OnPropertyChanged(_indexerChanged);
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
