using Avalonia.Input;
using Rove.UI.ViewModels;
using Xunit;

namespace Rove.UI.Tests;

/// <summary>Tabs, from the keys that open, close and move between them.</summary>
public class TabKeyTests : HeadlessTest
{
    private static void Fill(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, "alpha"));
        File.WriteAllText(Path.Combine(root, "alpha", "a.txt"), "a");
        File.WriteAllText(Path.Combine(root, "alpha", "b.txt"), "b");
        Directory.CreateDirectory(Path.Combine(root, "beta"));
        File.WriteAllText(Path.Combine(root, "beta", "c.txt"), "c");
        File.WriteAllText(Path.Combine(root, "one.txt"), "1");
        File.WriteAllText(Path.Combine(root, "two.txt"), "2");
    }

    private static int Cursor(WindowHarness harness, int tab) =>
        harness.Tabs.Items[tab].Content.DirectoryListing.ListSelection.Index;

    [Fact]
    public Task ThereIsOneTabToBeginWith() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);

        Assert.Single(harness.Tabs.Items);
        Assert.Equal(0, harness.Tabs.ActiveIndex);
        Assert.True(harness.Tabs.IsStripVisible);
    });

    [Fact]
    public Task CtrlTOpensASecondTabOnTheSameFolder() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);

        harness.Press(Key.T, RawInputModifiers.Control);

        Assert.Equal(2, harness.Tabs.Items.Count);
        Assert.Equal(1, harness.Tabs.ActiveIndex);
        Assert.Equal(harness.Root, harness.Content.DirectoryListing.CurrentDir);
        Assert.Contains("one.txt", harness.Names());
        Assert.True(harness.Tabs.IsStripVisible);
    });

    [Fact]
    public Task KeysActOnTheTabInFrontAndNotTheOneBehind() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        harness.Press(Key.T, RawInputModifiers.Control);
        Assert.Equal(0, Cursor(harness, 0));

        harness.Press(Key.J);

        Assert.Equal(1, Cursor(harness, 1));
        Assert.Equal(0, Cursor(harness, 0));
    });

    [Fact]
    public Task EachTabKeepsItsOwnFolder() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        harness.Press(Key.T, RawInputModifiers.Control);

        harness.Highlight("alpha");
        harness.Press(Key.Enter);

        Assert.Equal(Path.Combine(harness.Root, "alpha"), harness.Content.DirectoryListing.CurrentDir);
        Assert.Equal(harness.Root, harness.Tabs.Items[0].Content.DirectoryListing.CurrentDir);
    });

    [Fact]
    public Task MarksBelongToTheTabTheyWereMadeIn() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        harness.Press(Key.T, RawInputModifiers.Control);

        harness.Press(Key.V);

        Assert.Single(harness.Tabs.Items[1].Content.DirectoryListing.Items, i => i.IsMarked);
        Assert.DoesNotContain(harness.Tabs.Items[0].Content.DirectoryListing.Items, i => i.IsMarked);
    });

    [Fact]
    public Task CtrlTabWalksForwardAndWrapsAround() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        harness.Press(Key.T, RawInputModifiers.Control);
        harness.Press(Key.T, RawInputModifiers.Control);
        Assert.Equal(2, harness.Tabs.ActiveIndex);

        harness.Press(Key.Tab, RawInputModifiers.Control);
        Assert.Equal(0, harness.Tabs.ActiveIndex);

        harness.Press(Key.Tab, RawInputModifiers.Control);
        Assert.Equal(1, harness.Tabs.ActiveIndex);
    });

    [Fact]
    public Task CtrlShiftTabWalksBack() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        harness.Press(Key.T, RawInputModifiers.Control);
        harness.Press(Key.T, RawInputModifiers.Control);

        harness.Press(Key.Tab, RawInputModifiers.Control | RawInputModifiers.Shift);

        Assert.Equal(1, harness.Tabs.ActiveIndex);
    });

    [Fact]
    public Task AltNumberGoesStraightToATab() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        harness.Press(Key.T, RawInputModifiers.Control);
        harness.Press(Key.T, RawInputModifiers.Control);

        harness.Press(Key.D1, RawInputModifiers.Alt);

        Assert.Equal(0, harness.Tabs.ActiveIndex);
    });

    [Fact]
    public Task AltNumberPastTheLastTabLeavesYouWhereYouAre() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        harness.Press(Key.T, RawInputModifiers.Control);

        harness.Press(Key.D9, RawInputModifiers.Alt);

        Assert.Equal(1, harness.Tabs.ActiveIndex);
        Assert.Equal(2, harness.Tabs.Items.Count);
    });

    [Fact]
    public Task CtrlEnterOpensTheHighlightedFolderInANewTab() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        harness.Highlight("beta");

        harness.Press(Key.Enter, RawInputModifiers.Control);

        Assert.Equal(2, harness.Tabs.Items.Count);
        Assert.Equal(Path.Combine(harness.Root, "beta"), harness.Content.DirectoryListing.CurrentDir);
        Assert.Equal(["c.txt"], harness.Names());

        // The tab it was opened from has not moved.
        Assert.Equal(harness.Root, harness.Tabs.Items[0].Content.DirectoryListing.CurrentDir);
    });

    [Fact]
    public Task CtrlEnterOnAFileOpensTheFolderItSitsIn() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        harness.Highlight("one.txt");

        harness.Press(Key.Enter, RawInputModifiers.Control);

        Assert.Equal(2, harness.Tabs.Items.Count);
        Assert.Equal(harness.Root, harness.Content.DirectoryListing.CurrentDir);
    });

    [Fact]
    public Task CtrlWClosesTheTabInFront() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        harness.Press(Key.T, RawInputModifiers.Control);
        harness.Highlight("alpha");
        harness.Press(Key.Enter);

        harness.Press(Key.W, RawInputModifiers.Control);

        Assert.Single(harness.Tabs.Items);
        Assert.Equal(0, harness.Tabs.ActiveIndex);
        Assert.Equal(harness.Root, harness.Content.DirectoryListing.CurrentDir);
    });

    /// <summary>
    /// Closing one in the middle leaves the index where it was but a
    /// different tab under it, which is the case a strip is easiest to get
    /// wrong on.
    /// </summary>
    [Fact]
    public Task ClosingATabInTheMiddleBringsTheOneAfterItForward() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        harness.Press(Key.T, RawInputModifiers.Control);
        harness.Press(Key.T, RawInputModifiers.Control);
        harness.GoTo(Path.Combine(harness.Root, "beta"));
        harness.Press(Key.D2, RawInputModifiers.Alt);
        harness.GoTo(Path.Combine(harness.Root, "alpha"));

        harness.Press(Key.W, RawInputModifiers.Control);

        Assert.Equal(2, harness.Tabs.Items.Count);
        Assert.Equal(1, harness.Tabs.ActiveIndex);
        Assert.Equal(Path.Combine(harness.Root, "beta"), harness.Content.DirectoryListing.CurrentDir);
        Assert.Equal(["c.txt"], harness.Names());
    });

    [Fact]
    public Task ClosingTheLastTabAsksTheAppToClose() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        int asked = 0;
        harness.Tabs.Emptied += () => asked++;

        harness.Press(Key.W, RawInputModifiers.Control);

        Assert.Equal(1, asked);
        Assert.Single(harness.Tabs.Items);
    });

    [Fact]
    public Task KeysStillReachTheTabLeftAfterAClose() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        harness.Press(Key.T, RawInputModifiers.Control);
        harness.Press(Key.W, RawInputModifiers.Control);

        harness.Press(Key.J);

        Assert.Equal(1, Cursor(harness, 0));
    });

    [Fact]
    public Task ATabIsNamedAfterItsFolderAndFollowsIt() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        harness.Press(Key.T, RawInputModifiers.Control);
        Assert.Equal(Path.GetFileName(harness.Root), harness.Tabs.Items[1].Title);

        harness.Highlight("alpha");
        harness.Press(Key.Enter);

        Assert.Equal("alpha", harness.Tabs.Items[1].Title);
        Assert.Equal(Path.GetFileName(harness.Root), harness.Tabs.Items[0].Title);
    });

    [Fact]
    public Task OnlyTheTabInFrontIsMarkedActive() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        harness.Press(Key.T, RawInputModifiers.Control);

        Assert.False(harness.Tabs.Items[0].IsActive);
        Assert.True(harness.Tabs.Items[1].IsActive);

        harness.Press(Key.D1, RawInputModifiers.Alt);

        Assert.True(harness.Tabs.Items[0].IsActive);
        Assert.False(harness.Tabs.Items[1].IsActive);
    });

    [Fact]
    public Task TheStatusBarCountsTheTabInFront() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        harness.Press(Key.T, RawInputModifiers.Control);
        harness.GoTo(Path.Combine(harness.Root, "beta"));

        Assert.Contains("1 item", harness.Model.ItemSummary);

        harness.Press(Key.D1, RawInputModifiers.Alt);

        Assert.Contains("4 items", harness.Model.ItemSummary);
    });

    /// <summary>
    /// The undo history belongs to the app: what was done was done to the
    /// disk, not to a tab, and the tab it was done from is rarely what a
    /// person has in mind when they reach for undo.
    /// </summary>
    [Fact]
    public Task UndoReachesBackToWhatAnotherTabDid() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        harness.Highlight("one.txt");
        harness.Press(Key.Z);
        Assert.True(File.Exists(Path.Combine(harness.Root, "one.zip")), harness.Model.StatusLine);

        harness.Press(Key.T, RawInputModifiers.Control);
        harness.Press(Key.U);

        Assert.False(File.Exists(Path.Combine(harness.Root, "one.zip")), harness.Model.StatusLine);
    });

    [Fact]
    public Task ThePlusButtonOpensATabLikeCtrlTDoes() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);

        harness.Tabs.NewTabCommand.Execute(null);

        Assert.Equal(2, harness.Tabs.Items.Count);
        Assert.Equal(1, harness.Tabs.ActiveIndex);
        Assert.Equal(harness.Root, harness.Content.DirectoryListing.CurrentDir);
    });

    [Fact]
    public Task ClickingATabBringsItToFront() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        harness.Press(Key.T, RawInputModifiers.Control);
        Assert.Equal(1, harness.Tabs.ActiveIndex);

        harness.Tabs.Items[0].ActivateCommand.Execute(null);

        Assert.Equal(0, harness.Tabs.ActiveIndex);
        Assert.True(harness.Tabs.Items[0].IsActive);
        Assert.False(harness.Tabs.Items[1].IsActive);
    });

    /// <summary>The x closes whichever tab it is on, not just the one in front.</summary>
    [Fact]
    public Task TheXOnATabClosesItEvenWhenItIsNotInFront() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        harness.Press(Key.T, RawInputModifiers.Control);
        harness.GoTo(Path.Combine(harness.Root, "beta"));
        harness.Press(Key.D2, RawInputModifiers.Alt);
        harness.GoTo(Path.Combine(harness.Root, "alpha"));
        Assert.Equal(1, harness.Tabs.ActiveIndex);

        harness.Tabs.Items[0].CloseCommand.Execute(null);

        Assert.Single(harness.Tabs.Items);
        Assert.Equal(0, harness.Tabs.ActiveIndex);
        Assert.Equal(Path.Combine(harness.Root, "alpha"), harness.Content.DirectoryListing.CurrentDir);
    });

    [Fact]
    public Task ClosingTheLastTabByItsXAlsoAsksTheAppToClose() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        int asked = 0;
        harness.Tabs.Emptied += () => asked++;

        harness.Tabs.Items[0].CloseCommand.Execute(null);

        Assert.Equal(1, asked);
        Assert.Single(harness.Tabs.Items);
    });

    [Fact]
    public Task AltHGoesBackToWhereYouWereBefore() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        harness.GoTo(Path.Combine(harness.Root, "alpha"));
        harness.GoTo(Path.Combine(harness.Root, "beta"));

        harness.Press(Key.H, RawInputModifiers.Alt);

        Assert.Equal(Path.Combine(harness.Root, "alpha"), harness.Content.DirectoryListing.CurrentDir);
    });

    [Fact]
    public Task AltLGoesForwardAgainAfterGoingBack() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        harness.GoTo(Path.Combine(harness.Root, "alpha"));
        harness.GoTo(Path.Combine(harness.Root, "beta"));
        harness.Press(Key.H, RawInputModifiers.Alt);

        harness.Press(Key.L, RawInputModifiers.Alt);

        Assert.Equal(Path.Combine(harness.Root, "beta"), harness.Content.DirectoryListing.CurrentDir);
    });

    [Fact]
    public Task NavigatingAfterGoingBackDropsTheForwardHistory() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        harness.GoTo(Path.Combine(harness.Root, "alpha"));
        harness.GoTo(Path.Combine(harness.Root, "beta"));
        harness.Press(Key.H, RawInputModifiers.Alt);

        harness.GoTo(harness.Root);

        Assert.False(harness.Content.CanGoForward);
    });

    [Fact]
    public Task EachTabKeepsItsOwnHistory() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        harness.GoTo(Path.Combine(harness.Root, "alpha"));
        harness.Press(Key.T, RawInputModifiers.Control);
        Assert.False(harness.Content.CanGoBack);

        harness.Press(Key.D1, RawInputModifiers.Alt);

        Assert.True(harness.Content.CanGoBack);
    });

    [Fact]
    public Task ShiftRBeginsRenamingTheActiveTab() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);

        harness.Press(Key.R, RawInputModifiers.Shift);

        Assert.True(harness.Tabs.Items[0].IsRenaming);
        Assert.Equal(Path.GetFileName(harness.Root), harness.Tabs.Items[0].EditText);
    });

    [Fact]
    public Task RenameTabIsOfferedInThePalette() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);

        harness.Press(Key.Space);

        Assert.Contains(harness.Model.Palette.Items, e => e.Command.Def.Title == "Rename Tab");
    });

    [Fact]
    public Task EnterAppliesTheNewTabName() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        harness.Tabs.Items[0].BeginRename();
        harness.Tabs.Items[0].EditText = "Projects";

        harness.Press(Key.Enter);

        Assert.False(harness.Tabs.Items[0].IsRenaming);
        Assert.Equal("Projects", harness.Tabs.Items[0].Title);
    });

    [Fact]
    public Task EscapeCancelsARenameInProgress() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        harness.Tabs.Items[0].BeginRename();
        harness.Tabs.Items[0].EditText = "Projects";

        harness.Press(Key.Escape);

        Assert.False(harness.Tabs.Items[0].IsRenaming);
        Assert.Equal(Path.GetFileName(harness.Root), harness.Tabs.Items[0].Title);
    });

    [Fact]
    public Task ARenamedTabKeepsItsNameAfterNavigating() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        harness.Tabs.Items[0].BeginRename();
        harness.Tabs.Items[0].EditText = "Projects";
        harness.Press(Key.Enter);

        harness.GoTo(Path.Combine(harness.Root, "alpha"));

        Assert.Equal("Projects", harness.Tabs.Items[0].Title);
    });

    [Fact]
    public Task CtrlQStillQuits() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);

        // Nothing to assert about shutting down under a headless lifetime;
        // what matters is that the key is bound and nothing throws on it.
        harness.Press(Key.Q, RawInputModifiers.Control);

        Assert.Single(harness.Tabs.Items);
    });
}
