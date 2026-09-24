using Avalonia.Input;
using Xunit;

namespace Rove.UI.Tests;

public sealed class ClipboardKeyTests : HeadlessTest
{
    private static void Fill(string root)
    {
        File.WriteAllText(Path.Combine(root, "a.txt"), "hello");
        Directory.CreateDirectory(Path.Combine(root, "dest1"));
        Directory.CreateDirectory(Path.Combine(root, "dest2"));
    }

    [Fact]
    public Task CopyStaysStagedForRepeatedPastesAndShowsNoBanner() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        harness.Highlight("a.txt");
        harness.Press(Key.Y);

        Assert.False(harness.Model.IsTransferPending);

        harness.GoTo(Path.Combine(harness.Root, "dest1"));
        harness.Press(Key.P);
        Assert.Contains("a.txt", harness.Names());

        harness.GoTo(Path.Combine(harness.Root, "dest2"));
        harness.Press(Key.P);
        Assert.Contains("a.txt", harness.Names());
    });

    [Fact]
    public Task CopyToHereShowsABannerAndClearsAfterItsFirstPaste() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        harness.Highlight("a.txt");
        harness.Press(Key.Y, RawInputModifiers.Shift);

        Assert.True(harness.Model.IsTransferPending);

        harness.GoTo(Path.Combine(harness.Root, "dest1"));
        harness.Press(Key.P);
        Assert.Contains("a.txt", harness.Names());
        Assert.False(harness.Model.IsTransferPending);

        harness.GoTo(Path.Combine(harness.Root, "dest2"));
        harness.Press(Key.P);
        Assert.DoesNotContain("a.txt", harness.Names());
        Assert.Contains("nothing to paste", harness.Model.StatusLine, StringComparison.OrdinalIgnoreCase);
    });

    [Fact]
    public Task CutStaysStagedUntilPastedAndShowsNoBanner() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        harness.Highlight("a.txt");
        harness.Press(Key.X);

        Assert.False(harness.Model.IsTransferPending);

        harness.GoTo(Path.Combine(harness.Root, "dest1"));
        harness.Press(Key.P);
        Assert.Contains("a.txt", harness.Names());
    });

    [Fact]
    public Task MoveToHereShowsABannerAndClearsAfterItsFirstPaste() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        harness.Highlight("a.txt");
        harness.Press(Key.X, RawInputModifiers.Shift);

        Assert.True(harness.Model.IsTransferPending);

        harness.GoTo(Path.Combine(harness.Root, "dest1"));
        harness.Press(Key.P);
        Assert.Contains("a.txt", harness.Names());
        Assert.False(harness.Model.IsTransferPending);
    });
}
