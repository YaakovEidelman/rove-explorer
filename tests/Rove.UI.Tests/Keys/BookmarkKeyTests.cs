using Avalonia.Input;
using Rove.UI.Services;
using Rove.UI.ViewModels;
using Xunit;

namespace Rove.UI.Tests;

public class BookmarkKeyTests : HeadlessTest
{
    private static void Fill(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, "alpha"));
        Directory.CreateDirectory(Path.Combine(root, "alpha", "deep"));
        Directory.CreateDirectory(Path.Combine(root, "beta"));
        File.WriteAllText(Path.Combine(root, "notes.txt"), "hello");
    }

    private static void Keep(WindowHarness harness, string name)
    {
        harness.Highlight(name);
        harness.Press(Key.B);
    }

    [Fact]
    public Task BKeepsTheHighlightedItemAndPressingItAgainForgetsIt() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);

        Keep(harness, "alpha");
        Assert.Equal([Path.Combine(harness.Root, "alpha")], harness.Bookmarks.Items.Select(b => b.Path));

        harness.Press(Key.B);
        Assert.Empty(harness.Bookmarks.Items);
    });

    [Fact]
    public Task TheListIsOnlyEverOnScreenWhenAskedFor() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        Keep(harness, "alpha");
        Assert.False(harness.Model.Bookmarks.IsOpen);

        harness.Press(Key.B, RawInputModifiers.Shift);

        Assert.True(harness.Model.Bookmarks.IsOpen);
        Assert.Equal(Mode.Bookmarks, harness.Model.GetCurrentMode());
        Assert.Equal(["alpha"], harness.Model.Bookmarks.Items.Where(r => !r.IsAddNew).Select(r => r.Entry!.Bookmark.Name));

        harness.Press(Key.Escape);

        Assert.False(harness.Model.Bookmarks.IsOpen);
        Assert.Equal(Mode.Browse, harness.Model.GetCurrentMode());
    });

    [Fact]
    public Task CtrlNAndCtrlPWalkTheListAndEnterGoesThere() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        Keep(harness, "alpha");
        Keep(harness, "beta");

        harness.Press(Key.B, RawInputModifiers.Shift);
        Assert.Equal(1, harness.Model.Bookmarks.SelectedIndex);

        harness.Press(Key.N, RawInputModifiers.Control);
        Assert.Equal(2, harness.Model.Bookmarks.SelectedIndex);

        harness.Press(Key.P, RawInputModifiers.Control);
        Assert.Equal(1, harness.Model.Bookmarks.SelectedIndex);

        harness.Press(Key.N, RawInputModifiers.Control);
        harness.Press(Key.Enter);

        Assert.False(harness.Model.Bookmarks.IsOpen);
        Assert.Equal(Path.Combine(harness.Root, "beta"), harness.Content.DirectoryListing.CurrentDir);
    });

    [Fact]
    public Task TheFirstNineAnswerToCtrlNumber() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        Keep(harness, "alpha");
        Keep(harness, "beta");

        harness.Press(Key.D2, RawInputModifiers.Control);
        Assert.Equal(Path.Combine(harness.Root, "beta"), harness.Content.DirectoryListing.CurrentDir);

        harness.Press(Key.D1, RawInputModifiers.Control);
        Assert.Equal(Path.Combine(harness.Root, "alpha"), harness.Content.DirectoryListing.CurrentDir);
    });

    [Fact]
    public Task AShortcutWithNoBookmarkBehindItSaysSoAndGoesNowhere() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);

        harness.Press(Key.D3, RawInputModifiers.Control);

        Assert.Equal(harness.Root, harness.Content.DirectoryListing.CurrentDir);
        Assert.Contains("no bookmark 3", harness.Model.StatusLine, StringComparison.OrdinalIgnoreCase);
    });

    [Fact]
    public Task ABookmarkedFileLandsOnTheFileRatherThanOpeningIt() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        Keep(harness, "notes.txt");
        harness.GoTo(Path.Combine(harness.Root, "alpha"));

        harness.Press(Key.D1, RawInputModifiers.Control);

        Assert.Equal(harness.Root, harness.Content.DirectoryListing.CurrentDir);
        Assert.Equal("notes.txt", harness.Content.HighlightedItem!.Name);
    });

    [Fact]
    public Task TypingNarrowsTheListWithoutMovingAnyonesShortcut() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        Keep(harness, "alpha");
        Keep(harness, "beta");

        harness.Press(Key.B, RawInputModifiers.Shift);
        harness.Model.Bookmarks.SearchText = "beta";
        harness.Settle();

        BookmarkEntry only = Assert.Single(harness.Model.Bookmarks.Items, r => !r.IsAddNew).Entry!;
        Assert.Equal("beta", only.Bookmark.Name);
        Assert.Equal("Ctrl+2", only.Shortcut);
    });

    [Fact]
    public Task CtrlDForgetsTheHighlightedBookmarkAndLeavesTheListUp() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        Keep(harness, "alpha");
        Keep(harness, "beta");

        harness.Press(Key.B, RawInputModifiers.Shift);
        harness.Press(Key.D, RawInputModifiers.Control);

        Assert.True(harness.Model.Bookmarks.IsOpen);
        Assert.Equal(["beta"], harness.Bookmarks.Items.Select(b => b.Name));
        Assert.Equal(["beta"], harness.Model.Bookmarks.Items.Where(r => !r.IsAddNew).Select(r => r.Entry!.Bookmark.Name));
        Assert.Equal("Ctrl+1", harness.Model.Bookmarks.Items[1].Entry!.Shortcut);
    });

    [Fact]
    public Task WithNothingHighlightedItIsTheFolderItselfThatIsKept() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(_ => { });

        harness.Press(Key.B);

        Bookmark only = Assert.Single(harness.Bookmarks.Items);
        Assert.Equal(harness.Root, only.Path);
        Assert.True(only.IsDirectory);
    });
}
