using Avalonia.Input;
using Xunit;

namespace Rove.UI.Tests;

public class CompressKeyTests : HeadlessTest
{
    private static void Fill(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, "album"));
        File.WriteAllText(Path.Combine(root, "album", "one.txt"), "1");
        File.WriteAllText(Path.Combine(root, "notes.txt"), "hello");
    }

    [Fact]
    public Task ZipsTheHighlightedItemAndShowsTheZip() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        harness.Highlight("notes.txt");

        harness.Press(Key.Z);

        Assert.True(File.Exists(Path.Combine(harness.Root, "notes.zip")), harness.Model.StatusLine);
        Assert.Contains("notes.zip", harness.Names());
        Assert.Equal("notes.zip", harness.Content.HighlightedItem!.Name);
    });

    [Fact]
    public Task ShiftZMakesATarGzInstead() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        harness.Highlight("notes.txt");

        harness.Press(Key.Z, RawInputModifiers.Shift);

        Assert.True(File.Exists(Path.Combine(harness.Root, "notes.tar.gz")), harness.Model.StatusLine);
        Assert.Contains("notes.tar.gz", harness.Names());
    });

    [Fact]
    public Task ZipsEverythingMarkedIntoOneArchive() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        harness.Highlight("album");
        harness.Press(Key.V);
        harness.Highlight("notes.txt");
        harness.Press(Key.V);

        harness.Press(Key.Z);

        string folder = Path.GetFileName(harness.Root);
        Assert.True(File.Exists(Path.Combine(harness.Root, folder + ".zip")), harness.Model.StatusLine);
    });

    [Fact]
    public Task UndoTakesTheZipBackOut() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        harness.Highlight("notes.txt");
        harness.Press(Key.Z);
        Assert.True(File.Exists(Path.Combine(harness.Root, "notes.zip")), harness.Model.StatusLine);

        harness.Press(Key.U);

        Assert.False(File.Exists(Path.Combine(harness.Root, "notes.zip")));
        Assert.DoesNotContain("notes.zip", harness.Names());
    });

    [Fact]
    public Task ZippingAndThenExtractingComesBackToTheSameFiles() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        harness.Highlight("album");

        harness.Press(Key.Z);
        harness.Highlight("album.zip");
        harness.Press(Key.E);

        Assert.Equal("1", File.ReadAllText(
            Path.Combine(harness.Root, "album (2)", "album", "one.txt")));
    });
}
