using Rove.Core.Protocol;
using Rove.UI.ViewModels;
using Xunit;

namespace Rove.UI.Tests;

public class ListSelectionTests
{
    private static FolderItem Item(string name) =>
        new(name, "/test/" + name, FileAttributes.Normal, DateTime.UnixEpoch, false, 1, "");

    private static List<ListViewItem> Items(int count) =>
        [.. Enumerable.Range(0, count).Select(i => new ListViewItem(Item($"f{i:D2}"), 16, new NullIconCache()))];

    [Fact]
    public void OneColumnStillWrapsTopAndBottom()
    {
        List<ListViewItem> items = Items(3);
        ListSelection selection = new(items);
        selection.Select(0);

        selection.MoveUp();
        Assert.Equal(2, selection.SelectedItem is { } last ? items.IndexOf(last) : -1);

        selection.MoveDown();
        Assert.Equal(0, items.IndexOf(selection.SelectedItem!));
    }

    [Fact]
    public void DownStepsByAWholeRow()
    {
        List<ListViewItem> items = Items(10);
        ListSelection selection = new(items);
        selection.SetColumnsPerRow(4);
        selection.Select(0);

        selection.MoveDown();

        Assert.Equal(4, items.IndexOf(selection.SelectedItem!));
    }

    [Fact]
    public void UpStepsByAWholeRow()
    {
        List<ListViewItem> items = Items(10);
        ListSelection selection = new(items);
        selection.SetColumnsPerRow(4);
        selection.Select(6);

        selection.MoveUp();

        Assert.Equal(2, items.IndexOf(selection.SelectedItem!));
    }

    [Fact]
    public void DownAndUpReturnToWhereTheyStarted()
    {
        List<ListViewItem> items = Items(11);
        ListSelection selection = new(items);
        selection.SetColumnsPerRow(4);
        selection.Select(1);

        selection.MoveDown();
        selection.MoveUp();

        Assert.Equal(1, items.IndexOf(selection.SelectedItem!));
    }

    [Fact]
    public void DownStaysPutWhenThereIsNoRowBelow()
    {
        List<ListViewItem> items = Items(10);
        ListSelection selection = new(items);
        selection.SetColumnsPerRow(4);
        selection.Select(8);

        selection.MoveDown();

        Assert.Equal(8, items.IndexOf(selection.SelectedItem!));
    }

    [Fact]
    public void UpStaysPutOnTheTopRow()
    {
        List<ListViewItem> items = Items(10);
        ListSelection selection = new(items);
        selection.SetColumnsPerRow(4);
        selection.Select(2);

        selection.MoveUp();

        Assert.Equal(2, items.IndexOf(selection.SelectedItem!));
    }

    [Fact]
    public void RightWalksInReadingOrderAcrossARowEdge()
    {
        List<ListViewItem> items = Items(10);
        ListSelection selection = new(items);
        selection.SetColumnsPerRow(4);
        selection.Select(3);

        selection.MoveRight();

        Assert.Equal(4, items.IndexOf(selection.SelectedItem!));
    }

    [Fact]
    public void LeftAndRightStopAtEitherEndRatherThanWrap()
    {
        List<ListViewItem> items = Items(5);
        ListSelection selection = new(items);
        selection.Select(0);

        selection.MoveLeft();
        Assert.Equal(0, items.IndexOf(selection.SelectedItem!));

        selection.Select(4);
        selection.MoveRight();
        Assert.Equal(4, items.IndexOf(selection.SelectedItem!));
    }

    [Fact]
    public void ThereIsAlwaysAtLeastOneColumn()
    {
        List<ListViewItem> items = Items(5);
        ListSelection selection = new(items);
        selection.SetColumnsPerRow(0);
        selection.Select(0);

        selection.MoveDown();

        Assert.Equal(1, items.IndexOf(selection.SelectedItem!));
    }
}
