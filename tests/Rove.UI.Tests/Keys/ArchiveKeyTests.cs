using System.IO.Compression;
using Avalonia.Input;
using Xunit;

namespace Rove.UI.Tests;

public class ArchiveKeyTests : HeadlessTest
{
    private static string Zip(string path, params (string Name, string Content)[] entries)
    {
        using FileStream stream = new(path, FileMode.Create);
        using ZipArchive zip = new(stream, ZipArchiveMode.Create);
        foreach ((string name, string content) in entries)
        {
            ZipArchiveEntry entry = zip.CreateEntry(name);
            if (name.EndsWith('/'))
                continue;
            using StreamWriter writer = new(entry.Open());
            writer.Write(content);
        }
        return path;
    }

    private static void Fill(string root)
    {
        Zip(Path.Combine(root, "pack.zip"),
            ("notes.txt", "hello"), ("sub/b.txt", "two"), ("sub/deep/c.txt", "three"));
        File.WriteAllText(Path.Combine(root, "loose.txt"), "outside");
    }

    [Fact]
    public Task EnterOnAZipGoesIntoItInsteadOfOpeningIt() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        harness.Highlight("pack.zip");

        harness.Press(Key.Enter);

        Assert.Equal(Path.Combine(harness.Root, "pack.zip"), harness.Content.DirectoryListing.CurrentDir);
        Assert.Equal(["sub", "notes.txt"], harness.Names());
        Assert.True(harness.Content.InArchive);
    });

    [Fact]
    public Task FoldersInsideTheZipOpenLikeAnyOther() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        harness.Highlight("pack.zip");
        harness.Press(Key.Enter);

        harness.Content.DirectoryListing.ListSelection.SelectPath(
            Path.Combine(harness.Root, "pack.zip", "sub"));
        harness.Press(Key.Enter);

        Assert.Equal(["deep", "b.txt"], harness.Names());
    });

    [Fact]
    public Task GoingUpWalksOutThroughTheZipAndThenOutOfIt() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        harness.GoTo(Path.Combine(harness.Root, "pack.zip", "sub", "deep"));
        Assert.Equal(["c.txt"], harness.Names());

        harness.Press(Key.H);
        Assert.Equal(Path.Combine(harness.Root, "pack.zip", "sub"), harness.Content.DirectoryListing.CurrentDir);

        harness.Press(Key.H);
        Assert.Equal(Path.Combine(harness.Root, "pack.zip"), harness.Content.DirectoryListing.CurrentDir);
        Assert.True(harness.Content.InArchive);

        harness.Press(Key.H);
        Assert.Equal(harness.Root, harness.Content.DirectoryListing.CurrentDir);
        Assert.False(harness.Content.InArchive);
    });

    [Fact]
    public Task ComingBackOutLeavesTheZipHighlighted() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        harness.Highlight("pack.zip");
        harness.Press(Key.Enter);

        harness.Press(Key.H);

        Assert.Equal("pack.zip", harness.Content.HighlightedItem!.Name);
    });

    [Fact]
    public Task LeavingTheZipPutsTheListBackOnRealFiles() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        harness.GoTo(Path.Combine(harness.Root, "pack.zip"));
        Assert.True(harness.Content.InArchive);

        harness.GoTo(harness.Root);

        Assert.False(harness.Content.InArchive);
        Assert.Contains("loose.txt", harness.Names());
    });

    [Fact]
    public Task EnterOnAFileInsideTakesACopyOutAndSaysSo() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        harness.GoTo(Path.Combine(harness.Root, "pack.zip"));
        harness.Content.DirectoryListing.ListSelection.SelectPath(
            Path.Combine(harness.Root, "pack.zip", "notes.txt"));

        harness.Press(Key.Enter);

        Assert.Contains("copy", harness.Model.StatusLine, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not saved back", harness.Model.StatusLine, StringComparison.OrdinalIgnoreCase);
    });

    [Fact]
    public Task TheStatusBarSaysWhenYouAreInsideAZip() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);

        harness.GoTo(Path.Combine(harness.Root, "pack.zip"));

        Assert.Contains("in an archive", harness.Model.ItemSummary);
    });

    [Theory]
    [InlineData(Key.R, "Rename")]
    [InlineData(Key.D, "Delete")]
    [InlineData(Key.Y, "Copy")]
    [InlineData(Key.X, "Cut")]
    [InlineData(Key.P, "Paste")]
    [InlineData(Key.F, "New file")]
    [InlineData(Key.Z, "Compress")]
    [InlineData(Key.E, "Extract")]
    public Task VerbsThatWriteAreRefusedInsideAZip(Key key, string verb) => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        harness.GoTo(Path.Combine(harness.Root, "pack.zip"));
        harness.Content.DirectoryListing.ListSelection.SelectPath(
            Path.Combine(harness.Root, "pack.zip", "notes.txt"));

        harness.Press(key);

        Assert.Contains(verb, harness.Model.StatusLine, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("does not work inside an archive", harness.Model.StatusLine);
    });

    [Fact]
    public Task NothingInsideTheZipIsTouchedByARefusedVerb() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        long before = new FileInfo(Path.Combine(harness.Root, "pack.zip")).Length;
        harness.GoTo(Path.Combine(harness.Root, "pack.zip"));
        harness.Content.DirectoryListing.ListSelection.SelectPath(
            Path.Combine(harness.Root, "pack.zip", "notes.txt"));

        harness.Press(Key.D);

        Assert.Equal(before, new FileInfo(Path.Combine(harness.Root, "pack.zip")).Length);
        Assert.Equal(["sub", "notes.txt"], harness.Names());
    });

    [Fact]
    public Task ZippingStillWorksOutsideAZip() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        harness.Highlight("loose.txt");

        harness.Press(Key.Z);

        Assert.True(File.Exists(Path.Combine(harness.Root, "loose.zip")), harness.Model.StatusLine);
    });

    [Fact]
    public Task ExtractingStillWorksOnAZipFromOutside() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        harness.Highlight("pack.zip");

        harness.Press(Key.E);

        Assert.Equal("hello", File.ReadAllText(Path.Combine(harness.Root, "pack", "notes.txt")));
        Assert.Equal("two", File.ReadAllText(Path.Combine(harness.Root, "pack", "sub", "b.txt")));
    });

    [Fact]
    public Task TheseKeysStillDoTheirJobInsideAZip() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        harness.GoTo(Path.Combine(harness.Root, "pack.zip"));

        harness.Press(Key.V);
        Assert.Single(harness.Content.DirectoryListing.Items, i => i.IsMarked);

        harness.Press(Key.Escape);
        Assert.DoesNotContain(harness.Content.DirectoryListing.Items, i => i.IsMarked);
    });

    [Fact]
    public Task TheBarCompletesAPathThatRunsThroughAZip() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        harness.GoTo(Path.Combine(harness.Root, "pack.zip"));

        harness.Content.InEditPath = true;
        harness.Content.EditPathText = Path.Combine(harness.Root, "pack.zip", "su");
        harness.Settle();
        harness.Press(Key.Tab);

        Assert.StartsWith(
            Path.Combine(harness.Root, "pack.zip", "sub"),
            harness.Content.EditPathText);
    });

    [Fact]
    public Task ABookmarkLeadsBackIntoTheZip() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        string inside = Path.Combine(harness.Root, "pack.zip", "sub", "deep");
        harness.GoTo(Path.Combine(harness.Root, "pack.zip", "sub"));

        harness.Content.DirectoryListing.ListSelection.SelectPath(inside);
        harness.Press(Key.B);
        harness.GoTo(harness.Root);
        harness.Press(Key.D1, RawInputModifiers.Control);

        Assert.Equal(inside, harness.Content.DirectoryListing.CurrentDir);
        Assert.Equal(["c.txt"], harness.Names());
    });

    [Fact]
    public Task ADamagedZipSaysSoAndLeavesYouWhereYouWere() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(root =>
        {
            Fill(root);
            File.WriteAllText(Path.Combine(root, "broken.zip"), "not a zip at all");
        });
        harness.Highlight("broken.zip");

        harness.Press(Key.Enter);

        Assert.Equal(harness.Root, harness.Content.DirectoryListing.CurrentDir);
        Assert.Contains("damaged", harness.Model.StatusLine);
    });
}
