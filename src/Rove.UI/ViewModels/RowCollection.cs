using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace Rove.UI.ViewModels;

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
