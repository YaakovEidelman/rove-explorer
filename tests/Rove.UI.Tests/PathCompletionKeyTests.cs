using Avalonia.Input;
using Rove.UI.Services;
using Xunit;

namespace Rove.UI.Tests;

/// <summary>
/// Tab in the path bar, driven through the real window. The list it puts up
/// is a mode of its own, so these also guard the thing that mode has to get
/// right: Enter and Escape mean one thing while it is showing and another
/// once it is gone.
/// </summary>
public class PathCompletionKeyTests : HeadlessTest
{
    private static void Fill(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, "reports"));
        Directory.CreateDirectory(Path.Combine(root, "report-drafts"));
        Directory.CreateDirectory(Path.Combine(root, "archive"));
        File.WriteAllText(Path.Combine(root, "notes.txt"), "hello");
    }

    /// <summary>Opens the path bar holding <paramref name="typed"/>.</summary>
    private static void TypePath(WindowHarness harness, string typed)
    {
        harness.Press(Key.L, RawInputModifiers.Control);
        harness.Content.EditPathText = typed;
        harness.Settle();
    }

    [Fact]
    public Task TabOnOneMatchFinishesTheNameWithoutAList() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        TypePath(harness, Path.Combine(harness.Root, "arch"));

        harness.Press(Key.Tab);

        Assert.False(harness.Content.Completions.IsOpen);
        Assert.Equal(
            Path.Combine(harness.Root, "archive") + Path.DirectorySeparatorChar,
            harness.Content.EditPathText);
    });

    [Fact]
    public Task TabOnSeveralMatchesCarriesTheTextAsFarAsTheyAgreeAndShowsThem() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        TypePath(harness, Path.Combine(harness.Root, "rep"));

        harness.Press(Key.Tab);

        Assert.True(harness.Content.Completions.IsOpen);
        Assert.Equal(Mode.PathCompletion, harness.Model.GetCurrentMode());
        Assert.Equal(Path.Combine(harness.Root, "report"), harness.Content.EditPathText);
        Assert.Equal(
            ["report-drafts", "reports"],
            harness.Content.Completions.Items.Select(entry => entry.Name).Order().ToArray());
    });

    [Fact]
    public Task TabOnAFolderWithNothingTypedOffersWhatIsInIt() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        TypePath(harness, harness.Root + Path.DirectorySeparatorChar);

        harness.Press(Key.Tab);

        Assert.True(harness.Content.Completions.IsOpen);
        Assert.Equal(4, harness.Content.Completions.Items.Count);
        Assert.True(harness.Content.Completions.Items[0].IsDirectory);
    });

    [Fact]
    public Task CtrlNFillsEachNameIntoTheBarAndEnterGoesThere() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        TypePath(harness, Path.Combine(harness.Root, "rep"));
        harness.Press(Key.Tab);
        Assert.Equal(-1, harness.Content.Completions.SelectedIndex);

        harness.Press(Key.N, RawInputModifiers.Control);
        string first = harness.Content.Completions.Items[harness.Content.Completions.SelectedIndex].Name;
        Assert.Equal(
            Path.Combine(harness.Root, first) + Path.DirectorySeparatorChar,
            harness.Content.EditPathText);

        harness.Press(Key.N, RawInputModifiers.Control);
        string second = harness.Content.Completions.Items[harness.Content.Completions.SelectedIndex].Name;
        Assert.NotEqual(first, second);
        Assert.Equal(
            Path.Combine(harness.Root, second) + Path.DirectorySeparatorChar,
            harness.Content.EditPathText);
        Assert.True(harness.Content.Completions.IsOpen);

        harness.Press(Key.Enter);

        Assert.False(harness.Content.InEditPath);
        Assert.Equal(Mode.Browse, harness.Model.GetCurrentMode());
        Assert.Equal(
            Path.Combine(harness.Root, second),
            harness.Content.DirectoryListing.CurrentDir);
    });

    [Fact]
    public Task CtrlPFromNothingHighlightedFillsTheLastName() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        TypePath(harness, Path.Combine(harness.Root, "rep"));
        harness.Press(Key.Tab);

        harness.Press(Key.P, RawInputModifiers.Control);

        string last = harness.Content.Completions.Items[^1].Name;
        Assert.Equal(
            Path.Combine(harness.Root, last) + Path.DirectorySeparatorChar,
            harness.Content.EditPathText);
    });

    [Fact]
    public Task TabWhileTheListShowsKeepsCompletingInsteadOfMoving() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        TypePath(harness, Path.Combine(harness.Root, "rep"));
        harness.Press(Key.Tab);
        int before = harness.Content.Completions.SelectedIndex;

        harness.Press(Key.Tab);

        Assert.Equal(before, harness.Content.Completions.SelectedIndex);
    });

    [Fact]
    public Task EscapeClosesTheListFirstAndThePathBarSecond() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        TypePath(harness, Path.Combine(harness.Root, "rep"));
        harness.Press(Key.Tab);

        harness.Press(Key.Escape);
        Assert.False(harness.Content.Completions.IsOpen);
        Assert.True(harness.Content.InEditPath);

        harness.Press(Key.Escape);
        Assert.False(harness.Content.InEditPath);
    });

    [Fact]
    public Task OnceTheListIsGoneEnterGoesToTheTypedPathAgain() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        TypePath(harness, Path.Combine(harness.Root, "rep"));
        harness.Press(Key.Tab);

        harness.Press(Key.Escape);
        harness.Content.EditPathText = Path.Combine(harness.Root, "archive");
        harness.Settle();
        harness.Press(Key.Enter);

        Assert.False(harness.Content.Completions.IsOpen);
        Assert.Equal(Mode.Browse, harness.Model.GetCurrentMode());
        Assert.Equal(
            Path.Combine(harness.Root, "archive"),
            harness.Content.DirectoryListing.CurrentDir);
    });

    [Fact]
    public Task TabOnSomethingThatMatchesNothingLeavesTheTextAlone() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        string typed = Path.Combine(harness.Root, "zzz");
        TypePath(harness, typed);

        harness.Press(Key.Tab);

        Assert.False(harness.Content.Completions.IsOpen);
        Assert.Equal(typed, harness.Content.EditPathText);
    });
}
