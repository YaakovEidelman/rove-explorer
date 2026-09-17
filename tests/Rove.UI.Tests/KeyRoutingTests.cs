using Avalonia.Input;
using Rove.UI.Models;
using Rove.UI.Services;
using Xunit;

namespace Rove.UI.Tests;

/// <summary>
/// Keys as the user presses them: into the window, through whatever holds
/// focus, out the other side as a command. Everything below drives the real
/// window rather than calling the view model, because every regression these
/// guard against lived in the gap between the two.
/// </summary>
public class KeyRoutingTests : HeadlessTest
{
    private static void Fill(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, "inner"));
        Directory.CreateDirectory(Path.Combine(root, "inner", "deep"));
        File.WriteAllText(Path.Combine(root, "notes.txt"), "hello");
    }

    [Fact]
    public Task EnterWalksIntoTheHighlightedFolder() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        harness.Highlight("inner");

        harness.Press(Key.Enter);

        Assert.Equal(Path.Combine(harness.Root, "inner"), harness.Content.DirectoryListing.CurrentDir);
        Assert.Equal(["deep"], harness.Names());
    });

    [Fact]
    public Task EnterStillWalksInAfterTheCommandPaletteHasBeenUsed() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);

        harness.Press(Key.Space);
        Assert.True(harness.Model.Palette.IsPaletteOpen);
        harness.Press(Key.Escape);
        Assert.False(harness.Model.Palette.IsPaletteOpen);

        harness.Highlight("inner");
        harness.Press(Key.Enter);

        Assert.Equal(Path.Combine(harness.Root, "inner"), harness.Content.DirectoryListing.CurrentDir);
    });

    [Fact]
    public Task EnterStillWalksInAfterTheFilterBoxHasBeenUsed() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);

        harness.Press(Key.OemQuestion);
        Assert.True(harness.Content.DirectoryListing.InLocalSearch);
        harness.Press(Key.Escape);
        Assert.False(harness.Content.DirectoryListing.InLocalSearch);

        harness.Highlight("inner");
        harness.Press(Key.Enter);

        Assert.Equal(Path.Combine(harness.Root, "inner"), harness.Content.DirectoryListing.CurrentDir);
    });

    [Fact]
    public Task EnterRunsTheHighlightedPaletteCommand() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);

        harness.Press(Key.Space);
        harness.Model.Palette.PaletteSearchText = "Show/Hide Hidden";
        harness.Settle();
        harness.Press(Key.Enter);

        Assert.False(harness.Model.Palette.IsPaletteOpen);
        Assert.True(harness.Content.DirectoryListing.ShowHidden);
    });

    [Fact]
    public Task SortsByTheActiveColumnFromResizeColumnsMode() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);

        harness.Press(Key.W);
        Assert.Equal(SortKey.Name, harness.Content.DirectoryListing.SortBy);
        Assert.False(harness.Content.DirectoryListing.SortDescending);

        harness.Press(Key.S);
        Assert.Equal(SortKey.Name, harness.Content.DirectoryListing.SortBy);
        Assert.True(harness.Content.DirectoryListing.SortDescending);

        harness.Press(Key.Tab);
        harness.Press(Key.S);
        Assert.Equal(SortKey.Type, harness.Content.DirectoryListing.SortBy);
    });

    [Fact]
    public Task CommaOpensAndClosesSettings() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);

        harness.Press(Key.OemComma);
        Assert.True(harness.Model.Settings.IsOpen);

        harness.Press(Key.Escape);
        Assert.False(harness.Model.Settings.IsOpen);
    });

    [Fact]
    public Task EnterLeavesTheFilterBoxButKeepsTheFilterApplied() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);

        harness.Press(Key.OemQuestion);
        harness.Content.DirectoryListing.SearchCurrentDirectoryText = "inner";
        harness.Settle();
        harness.Press(Key.Enter);

        Assert.Equal(harness.Root, harness.Content.DirectoryListing.CurrentDir);
        Assert.False(harness.Content.DirectoryListing.InLocalSearch);
        Assert.Equal(Mode.Browse, harness.Model.GetCurrentMode());
        Assert.Equal(["inner"], harness.Names());
    });

    [Fact]
    public Task OpeningAnItemAfterLeavingTheFilterBoxClearsTheFilter() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);

        harness.Press(Key.OemQuestion);
        harness.Content.DirectoryListing.SearchCurrentDirectoryText = "inner";
        harness.Settle();
        harness.Press(Key.Enter); // leaves the box, filter stays, "inner" still highlighted

        harness.Press(Key.Enter); // opens "inner"

        Assert.Equal(Path.Combine(harness.Root, "inner"), harness.Content.DirectoryListing.CurrentDir);
        Assert.Equal("", harness.Content.DirectoryListing.SearchCurrentDirectoryText);
    });

    [Fact]
    public Task EscAfterLeavingTheFilterBoxClearsIt() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);

        harness.Press(Key.OemQuestion);
        harness.Content.DirectoryListing.SearchCurrentDirectoryText = "inner";
        harness.Settle();
        harness.Press(Key.Enter); // leaves the box, filter stays

        harness.Press(Key.Escape);

        Assert.Equal("", harness.Content.DirectoryListing.SearchCurrentDirectoryText);
        Assert.Equal(["inner", "notes.txt"], harness.Names());
    });

    [Fact]
    public Task EnterAppliesARename() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        harness.Highlight("notes.txt");

        harness.Press(Key.R);
        Assert.Equal(Mode.RenameItem, harness.Model.GetCurrentMode());
        harness.Content.HighlightedItem!.EditText = "renamed.txt";
        harness.Press(Key.Enter);

        Assert.True(File.Exists(Path.Combine(harness.Root, "renamed.txt")));
        Assert.Equal(Mode.Browse, harness.Model.GetCurrentMode());
    });

    [Fact]
    public Task EnterCreatesTheNewFolder() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);

        harness.Press(Key.F, RawInputModifiers.Shift);
        Assert.Equal(Mode.CreateItem, harness.Model.GetCurrentMode());
        harness.Content.CreateItemText = "made";
        harness.Press(Key.Enter);

        Assert.True(Directory.Exists(Path.Combine(harness.Root, "made")));
    });

    [Fact]
    public Task EnterGoesToTheTypedPath() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);

        harness.Press(Key.L, RawInputModifiers.Control);
        Assert.Equal(Mode.EditPath, harness.Model.GetCurrentMode());
        harness.Content.EditPathText = Path.Combine(harness.Root, "inner");
        harness.Press(Key.Enter);

        Assert.Equal(Path.Combine(harness.Root, "inner"), harness.Content.DirectoryListing.CurrentDir);
    });

    [Fact]
    public Task HAndJAndKStillMoveAroundAfterASurfaceHasHadFocus() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);

        harness.Press(Key.Space);
        harness.Press(Key.Escape);

        harness.Press(Key.J);
        Assert.Equal("notes.txt", harness.Content.HighlightedItem!.Name);
        harness.Press(Key.K);
        Assert.Equal("inner", harness.Content.HighlightedItem!.Name);

        harness.Press(Key.Enter);
        Assert.Equal(Path.Combine(harness.Root, "inner"), harness.Content.DirectoryListing.CurrentDir);
        harness.Press(Key.H);
        Assert.Equal(harness.Root, harness.Content.DirectoryListing.CurrentDir);
    });
}
