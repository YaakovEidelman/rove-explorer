using Avalonia.Input;
using Rove.UI.Models;
using Xunit;

namespace Rove.UI.Tests;

/// <summary>The list/icon toggle and grid movement, driven through real keys and real layout.</summary>
public class ViewModeKeyTests : HeadlessTest
{
    private static void Fill(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, "inner"));
        for (int i = 0; i < 40; i++)
            File.WriteAllText(Path.Combine(root, $"file{i:D2}.txt"), i.ToString());
    }

    [Fact]
    public Task ICyclesThroughListAndEachIconSize() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        Assert.Equal(ContentViewMode.List, harness.Content.ViewMode);

        harness.Press(Key.I);
        Assert.Equal(ContentViewMode.Icons, harness.Content.ViewMode);
        Assert.Equal(IconSize.Small, harness.Content.IconSize);

        harness.Press(Key.I);
        Assert.Equal(IconSize.Medium, harness.Content.IconSize);

        harness.Press(Key.I);
        Assert.Equal(IconSize.Large, harness.Content.IconSize);

        harness.Press(Key.I);
        Assert.Equal(ContentViewMode.List, harness.Content.ViewMode);
    });

    [Fact]
    public Task JMovesByAWholeRowInIconView() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        harness.Press(Key.I);
        harness.Highlight("file00.txt");
        int start = harness.Content.DirectoryListing.ListSelection.Index;

        harness.Press(Key.J);

        int afterDown = harness.Content.DirectoryListing.ListSelection.Index;
        Assert.True(afterDown > start + 1, $"expected a row skip, landed on index {afterDown}");

        harness.Press(Key.K);

        Assert.Equal(start, harness.Content.DirectoryListing.ListSelection.Index);
    });

    [Fact]
    public Task JStaysPutOnTheLastRowInIconView() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        harness.Press(Key.I);
        harness.Highlight("file39.txt");
        int last = harness.Content.DirectoryListing.ListSelection.Index;

        harness.Press(Key.J);

        Assert.Equal(last, harness.Content.DirectoryListing.ListSelection.Index);
    });

    [Fact]
    public Task HAndLStepLeftAndRightInIconViewInsteadOfNavigating() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        harness.Press(Key.I);
        harness.Highlight("file01.txt");

        harness.Press(Key.L);
        Assert.Equal("file02.txt", harness.Content.HighlightedItem!.Name);
        Assert.Equal(harness.Root, harness.Content.DirectoryListing.CurrentDir);

        harness.Press(Key.H);
        harness.Press(Key.H);
        Assert.Equal("file00.txt", harness.Content.HighlightedItem!.Name);
        Assert.Equal(harness.Root, harness.Content.DirectoryListing.CurrentDir);
    });

    [Fact]
    public Task HAndLStillNavigateInListView() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        harness.Highlight("inner");

        harness.Press(Key.L);
        Assert.Equal(Path.Combine(harness.Root, "inner"), harness.Content.DirectoryListing.CurrentDir);

        harness.Press(Key.H);
        Assert.Equal(harness.Root, harness.Content.DirectoryListing.CurrentDir);
    });

    [Fact]
    public Task BackspaceStillGoesUpADirectoryFromIconView() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        harness.Press(Key.I);

        harness.Press(Key.Back);

        Assert.Equal(Path.GetDirectoryName(harness.Root), harness.Content.DirectoryListing.CurrentDir);
    });

    [Fact]
    public Task SwitchingBackToListMovesOneItemAtATimeAgain() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        harness.Press(Key.I);
        harness.Press(Key.I);
        harness.Press(Key.I);
        harness.Press(Key.I);
        Assert.Equal(ContentViewMode.List, harness.Content.ViewMode);
        harness.Highlight("file00.txt");

        harness.Press(Key.J);

        Assert.Equal("file01.txt", harness.Content.HighlightedItem!.Name);
    });
}
