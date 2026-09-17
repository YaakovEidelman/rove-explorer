using Avalonia.Input;
using Rove.UI.Services;
using Rove.UI.ViewModels;
using Xunit;

namespace Rove.UI.Tests;

public class PickerWindowKeyTests : HeadlessTest
{
    [Fact]
    public Task OpeningShowsTheStartDirectorysContents() => OnUiThread(() =>
    {
        using PickerWindowHarness harness = PickerWindowHarness.Open(root =>
        {
            File.WriteAllText(Path.Combine(root, "report.txt"), "");
            Directory.CreateDirectory(Path.Combine(root, "Photos"));
        });

        Assert.Equal(["Photos", "report.txt"], harness.Names());
    });

    [Fact]
    public Task EnterOnAFileConfirmsItAndExits() => OnUiThread(() =>
    {
        using PickerWindowHarness harness = PickerWindowHarness.Open(root =>
            File.WriteAllText(Path.Combine(root, "report.txt"), ""));
        harness.Highlight("report.txt");

        harness.Press(Key.Enter);

        Assert.Equal([0], harness.ExitCodes);
        Assert.Equal([Path.Combine(harness.Root, "report.txt")], harness.OutputLines());
    });

    [Fact]
    public Task EnterOnAFolderNavigatesInsteadOfConfirming() => OnUiThread(() =>
    {
        using PickerWindowHarness harness = PickerWindowHarness.Open(root =>
        {
            Directory.CreateDirectory(Path.Combine(root, "Photos"));
            File.WriteAllText(Path.Combine(root, "Photos", "cat.png"), "");
        });
        harness.Highlight("Photos");

        harness.Press(Key.Enter);

        Assert.Empty(harness.ExitCodes);
        Assert.Equal(["cat.png"], harness.Names());
    });

    [Fact]
    public Task EscapeCancelsWithoutWritingAnything() => OnUiThread(() =>
    {
        using PickerWindowHarness harness = PickerWindowHarness.Open(root =>
            File.WriteAllText(Path.Combine(root, "report.txt"), ""));

        harness.Press(Key.Escape);

        Assert.Equal([PickerWindowViewModel.CancelExitCode], harness.ExitCodes);
        Assert.False(File.Exists(harness.OutputFile));
    });

    [Fact]
    public Task MarkingSeveralFilesAndConfirmingWritesAllOfThem() => OnUiThread(() =>
    {
        using PickerWindowHarness harness = PickerWindowHarness.Open(root =>
        {
            File.WriteAllText(Path.Combine(root, "a.txt"), "");
            File.WriteAllText(Path.Combine(root, "b.txt"), "");
        }, multiple: true);
        harness.Highlight("a.txt");
        harness.Press(Key.V);
        harness.Highlight("b.txt");
        harness.Press(Key.V);

        harness.Press(Key.Enter);

        Assert.Equal([0], harness.ExitCodes);
        Assert.Equal(
            [Path.Combine(harness.Root, "a.txt"), Path.Combine(harness.Root, "b.txt")],
            [.. harness.OutputLines().OrderBy(l => l, StringComparer.Ordinal)]);
    });

    [Fact]
    public Task CtrlOInDirectoryModeChoosesTheCurrentFolder() => OnUiThread(() =>
    {
        using PickerWindowHarness harness = PickerWindowHarness.Open(
            root => Directory.CreateDirectory(Path.Combine(root, "Photos")), directory: true);
        harness.GoTo(Path.Combine(harness.Root, "Photos"));

        harness.Press(Key.O, RawInputModifiers.Control);

        Assert.Equal([0], harness.ExitCodes);
        Assert.Equal([Path.Combine(harness.Root, "Photos")], harness.OutputLines());
    });

    [Fact]
    public Task EnterOnAFileDoesNothingInDirectoryMode() => OnUiThread(() =>
    {
        using PickerWindowHarness harness = PickerWindowHarness.Open(
            root => File.WriteAllText(Path.Combine(root, "report.txt"), ""), directory: true);
        harness.Highlight("report.txt");

        harness.Press(Key.Enter);

        Assert.Empty(harness.ExitCodes);
    });

    [Fact]
    public Task CtrlFCyclesBetweenFilters() => OnUiThread(() =>
    {
        PickerFilter[] filters =
        [
            new("Text", ["*.txt"]),
            new("Images", ["*.png"]),
        ];
        using PickerWindowHarness harness = PickerWindowHarness.Open(root =>
        {
            File.WriteAllText(Path.Combine(root, "report.txt"), "");
            File.WriteAllText(Path.Combine(root, "cat.png"), "");
        }, filters: filters);

        Assert.Equal(["report.txt"], harness.Names());

        harness.Press(Key.F, RawInputModifiers.Control);

        Assert.Equal(["cat.png"], harness.Names());
    });
}
