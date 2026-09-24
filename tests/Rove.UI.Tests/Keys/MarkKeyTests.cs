using Avalonia.Input;
using Xunit;

namespace Rove.UI.Tests;

public class MarkKeyTests : HeadlessTest
{
    private static void Fill(string root)
    {
        for (int i = 0; i < 5; i++)
            File.WriteAllText(Path.Combine(root, $"file{i}.txt"), i.ToString());
    }

    private static string[] MarkedNames(WindowHarness harness) =>
        [.. harness.Content.DirectoryListing.Items.Where(i => i.IsMarked).Select(i => i.Name).OrderBy(n => n)];

    [Fact]
    public Task CtrlAMarksEveryItem() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        harness.Highlight("file0.txt");

        harness.Press(Key.A, RawInputModifiers.Control);

        Assert.Equal(harness.Names().OrderBy(n => n), MarkedNames(harness));
    });

    [Fact]
    public Task CtrlDDuplicatesTheHighlightedFileNextToItself() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        harness.Highlight("file0.txt");

        harness.Press(Key.D, RawInputModifiers.Control);

        Assert.Contains("file0 (copy).txt", harness.Names());
        Assert.True(File.Exists(Path.Combine(harness.Root, "file0.txt")));
    });
}
