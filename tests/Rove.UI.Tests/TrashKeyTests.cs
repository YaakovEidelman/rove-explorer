using Avalonia.Input;
using Rove.Core.Services;
using Xunit;

namespace Rove.UI.Tests;

/// <summary>
/// Browsing the trash inside Rove: the path bar hides where it really lives,
/// and most verbs that write are refused there — you look and put back, or
/// delete for good, and nothing else.
/// </summary>
public sealed class TrashKeyTests : HeadlessTest, IDisposable
{
    private readonly string? _previousDataHome;
    private readonly string _dataHome;

    public TrashKeyTests()
    {
        _previousDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        _dataHome = Path.Combine(Path.GetTempPath(), "rove-ui-trash-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dataHome);
        Environment.SetEnvironmentVariable("XDG_DATA_HOME", _dataHome);
    }

    public void Dispose() => Environment.SetEnvironmentVariable("XDG_DATA_HOME", _previousDataHome);

    private static void Fill(string root)
    {
        File.WriteAllText(Path.Combine(root, "loose.txt"), "outside");
    }

    private static void TrashOne(string path) => Assert.True(TrashService.MoveToTrash([path]).IsOk);

    private static string TrashRoot => TrashService.BrowsePath ?? throw new InvalidOperationException();

    [Fact]
    public Task TShowsTheTrashAsASingleCrumbNotTheRealPath() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        string loose = Path.Combine(harness.Root, "loose.txt");
        TrashOne(loose);

        harness.Press(Key.T);

        Assert.Equal(TrashService.BrowsePath, harness.Content.DirectoryListing.CurrentDir);
        Assert.True(harness.Content.InTrash);
        Assert.Equal(["Trash"], [.. harness.Content.DirectoryListing.Crumbs.Select(c => c.Label)]);
        Assert.Contains("loose.txt", harness.Names());
    });

    [Fact]
    public Task BrowsingIntoATrashedFolderKeepsTheTrashCrumbAndAddsRealSteps() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(root =>
        {
            string folder = Path.Combine(root, "OldProject");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "a.txt"), "keep");
        });
        TrashOne(Path.Combine(harness.Root, "OldProject"));
        harness.Press(Key.T);

        // Trash isn't the harness's root, so Highlight (root-relative) can't
        // reach into it — select the trashed folder by its real path instead.
        harness.Content.DirectoryListing.ListSelection.SelectPath(
            Path.Combine(TrashRoot, "OldProject"));
        harness.Press(Key.Enter);

        Assert.Equal(
            ["Trash", "OldProject"],
            [.. harness.Content.DirectoryListing.Crumbs.Select(c => c.Label)]);
        Assert.Contains("a.txt", harness.Names());
    });

    [Theory]
    [InlineData(Key.R, "Rename")]
    [InlineData(Key.X, "Cut")]
    [InlineData(Key.P, "Paste")]
    [InlineData(Key.F, "New file")]
    [InlineData(Key.Z, "Compress")]
    [InlineData(Key.E, "Extract")]
    public Task VerbsThatWriteAreRefusedInsideTheTrash(Key key, string verb) => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        TrashOne(Path.Combine(harness.Root, "loose.txt"));
        harness.Press(Key.T);
        harness.Content.DirectoryListing.ListSelection.SelectPath(
            Path.Combine(TrashRoot, "loose.txt"));

        harness.Press(key);

        Assert.Contains(verb, harness.Model.StatusLine, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"does not work inside the {TrashService.DisplayName}", harness.Model.StatusLine);
    });

    [Fact]
    public Task PlainDeleteIsRefusedButSaysToDeletePermanently() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        string trashedPath = Path.Combine(TrashRoot, "loose.txt");
        TrashOne(Path.Combine(harness.Root, "loose.txt"));
        harness.Press(Key.T);
        harness.Content.DirectoryListing.ListSelection.SelectPath(trashedPath);

        harness.Press(Key.D);

        Assert.Contains("delete permanently", harness.Model.StatusLine, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(trashedPath));
    });

    [Fact]
    public Task PermanentDeleteStillWorksInsideTheTrash() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        string trashedPath = Path.Combine(TrashRoot, "loose.txt");
        TrashOne(Path.Combine(harness.Root, "loose.txt"));
        harness.Press(Key.T);
        harness.Content.DirectoryListing.ListSelection.SelectPath(trashedPath);

        harness.Press(Key.D, RawInputModifiers.Shift);
        harness.Press(Key.Y);

        Assert.False(File.Exists(trashedPath));
    });

    [Fact]
    public Task RestoreSelectedStillWorksInsideTheTrash() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        string original = Path.Combine(harness.Root, "loose.txt");
        TrashOne(original);
        harness.Press(Key.T);
        harness.Content.DirectoryListing.ListSelection.SelectPath(
            Path.Combine(TrashRoot, "loose.txt"));

        harness.Press(Key.U, RawInputModifiers.Shift);

        Assert.True(File.Exists(original));
    });

    [Fact]
    public Task RestoreAllPutsEverythingBackAndReturnsToTheTrashRoot() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(root =>
        {
            File.WriteAllText(Path.Combine(root, "one.txt"), "1");
            File.WriteAllText(Path.Combine(root, "two.txt"), "2");
        });
        TrashOne(Path.Combine(harness.Root, "one.txt"));
        TrashOne(Path.Combine(harness.Root, "two.txt"));
        harness.Press(Key.T);

        harness.Press(Key.U, RawInputModifiers.Control | RawInputModifiers.Shift);

        Assert.True(File.Exists(Path.Combine(harness.Root, "one.txt")));
        Assert.True(File.Exists(Path.Combine(harness.Root, "two.txt")));
        Assert.Equal(TrashService.BrowsePath, harness.Content.DirectoryListing.CurrentDir);
        Assert.Empty(harness.Names());
    });

    [Fact]
    public Task EmptyTrashDeletesEverythingForGoodAfterConfirming() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(root =>
        {
            File.WriteAllText(Path.Combine(root, "one.txt"), "1");
            File.WriteAllText(Path.Combine(root, "two.txt"), "2");
        });
        TrashOne(Path.Combine(harness.Root, "one.txt"));
        TrashOne(Path.Combine(harness.Root, "two.txt"));
        harness.Press(Key.T);

        harness.Press(Key.D, RawInputModifiers.Control | RawInputModifiers.Shift);
        harness.Press(Key.Y);

        Assert.False(File.Exists(Path.Combine(TrashRoot, "one.txt")));
        Assert.False(File.Exists(Path.Combine(TrashRoot, "two.txt")));
        Assert.Empty(harness.Names());
    });
}
