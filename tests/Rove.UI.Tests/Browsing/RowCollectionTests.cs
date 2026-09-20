using System.Collections.Specialized;
using System.ComponentModel;
using Rove.Core.Protocol;
using Rove.UI.ViewModels;
using Xunit;

namespace Rove.UI.Tests;

public class RowCollectionTests
{
    private static ListViewItem Row(string name) =>
        new(new FolderItem(name, "/test/" + name, FileAttributes.Normal, DateTime.UnixEpoch, false, 1, ".txt"),
            16, new NullIconCache());

    [Fact]
    public void ResetToReplacesEverythingWithOneResetNotification()
    {
        RowCollection rows = new();
        rows.ResetTo([Row("a.txt"), Row("b.txt")]);

        List<NotifyCollectionChangedAction> actions = [];
        rows.CollectionChanged += (_, e) => actions.Add(e.Action);

        rows.ResetTo([Row("c.txt")]);

        Assert.Equal(["c.txt"], rows.Select(r => r.Name));
        Assert.Equal([NotifyCollectionChangedAction.Reset], actions);
    }

    [Fact]
    public void ResetToRaisesCountAndIndexerChanged()
    {
        RowCollection rows = new();
        List<string> changed = [];
        ((INotifyPropertyChanged)rows).PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        rows.ResetTo([Row("a.txt")]);

        Assert.Contains(nameof(RowCollection.Count), changed);
        Assert.Contains("Item[]", changed);
    }
}
