using Avalonia.Input;
using Rove.UI.Services;
using Xunit;

namespace Rove.UI.Tests;

public class TabSessionTests : HeadlessTest
{
    private static void Fill(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, "alpha"));
        Directory.CreateDirectory(Path.Combine(root, "beta"));
    }

    [Fact]
    public Task SnapshotCapturesEveryTabsFolderNameAndActiveIndex() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        harness.Press(Key.T, RawInputModifiers.Control);
        harness.GoTo(Path.Combine(harness.Root, "alpha"));
        harness.Tabs.Items[0].BeginRename();
        harness.Tabs.Items[0].EditText = "Home Base";
        harness.Press(Key.Enter);

        AppSession snapshot = harness.Tabs.Snapshot();

        Assert.Equal(2, snapshot.Tabs.Length);
        Assert.Equal(harness.Root, snapshot.Tabs[0].Directory);
        Assert.Equal("Home Base", snapshot.Tabs[0].Title);
        Assert.Equal(Path.Combine(harness.Root, "alpha"), snapshot.Tabs[1].Directory);
        Assert.Null(snapshot.Tabs[1].Title);
        Assert.Equal(1, snapshot.ActiveIndex);
    });

    [Fact]
    public Task RestoreReopensEveryRememberedTabWithItsName() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        AppSession session = new(
            [new(Path.Combine(harness.Root, "alpha"), "Alpha"), new(Path.Combine(harness.Root, "beta"))],
            ActiveIndex: 0);

        harness.Tabs.Restore(session);
        harness.Settle();

        Assert.Equal(3, harness.Tabs.Items.Count);
        Assert.Equal(Path.Combine(harness.Root, "alpha"), harness.Tabs.Items[1].Content.DirectoryListing.CurrentDir);
        Assert.Equal("Alpha", harness.Tabs.Items[1].Title);
        Assert.Equal(Path.Combine(harness.Root, "beta"), harness.Tabs.Items[2].Content.DirectoryListing.CurrentDir);
        Assert.Equal(0, harness.Tabs.ActiveIndex);
    });

    [Fact]
    public Task RestoreSkipsARememberedFolderThatIsGoneNow() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        AppSession session = new([new(Path.Combine(harness.Root, "gone-now"))]);

        harness.Tabs.Restore(session);
        harness.Settle();

        Assert.Single(harness.Tabs.Items);
    });
}
