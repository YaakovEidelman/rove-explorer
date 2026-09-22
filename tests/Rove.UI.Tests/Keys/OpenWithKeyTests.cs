using Avalonia.Input;
using Rove.UI.Services;
using Rove.UI.ViewModels;
using Xunit;

namespace Rove.UI.Tests;

public class OpenWithKeyTests : HeadlessTest
{
    private static void Fill(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, "docs"));
        File.WriteAllText(Path.Combine(root, "notes.txt"), "hello");
    }

    private static void RunOpenWith(WindowHarness harness)
    {
        harness.Press(Key.Space);
        harness.Model.Palette.PaletteSearchText = "Open With";
        harness.Settle();
        Assert.Equal("Open With…", harness.Model.Palette.Items[0].Command.Def.Title);
        harness.Press(Key.Enter);
    }

    private static bool IsOthers(PaletteEntry entry) => entry.Command.Def.Id == CommandDef.OpenWithOthersId;

    [Fact]
    public Task OpenWithFromThePaletteListsOnlyAppsAndEndsInOtherApps() => OnUiThread(() =>
    {
        if (!OperatingSystem.IsLinux())
            return;

        using WindowHarness harness = WindowHarness.Open(Fill);
        harness.Highlight("notes.txt");

        RunOpenWith(harness);
        for (int wait = 0; wait < 200 && !harness.Model.Palette.Items.Any(IsOthers); wait++)
        {
            harness.Settle();
            Thread.Sleep(20);
        }

        Assert.True(harness.Model.Palette.IsPaletteOpen);
        Assert.True(IsOthers(harness.Model.Palette.Items[^1]));
        Assert.All(
            harness.Model.Palette.Items,
            entry => Assert.StartsWith(CommandDef.OpenWithIdPrefix, entry.Command.Def.Id));
    });

    [Fact]
    public Task HandingFromOnePickerToAnotherNeverFlipsTheCardClosed() => OnUiThread(() =>
    {
        if (!OperatingSystem.IsLinux())
            return;

        using WindowHarness harness = WindowHarness.Open(Fill);
        harness.Highlight("notes.txt");
        harness.Press(Key.Space);
        List<bool> seen = [];
        harness.Model.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(harness.Model.IsQuickAccessOpen))
                seen.Add(harness.Model.IsQuickAccessOpen);
        };

        harness.Model.Palette.PaletteSearchText = "Open With";
        harness.Settle();
        harness.Press(Key.Enter);
        for (int wait = 0; wait < 200 && !harness.Model.Palette.Items.Any(IsOthers); wait++)
        {
            harness.Settle();
            Thread.Sleep(20);
        }
        harness.Press(Key.P, RawInputModifiers.Control);
        Assert.True(IsOthers(harness.Model.Palette.Items[harness.Model.Palette.SelectedIndex]));
        harness.Press(Key.Enter);
        for (int wait = 0; wait < 200 && harness.Model.Palette.Items.Any(IsOthers); wait++)
        {
            harness.Settle();
            Thread.Sleep(20);
        }

        Assert.True(harness.Model.Palette.IsPaletteOpen);
        Assert.True(harness.Model.IsQuickAccessOpen);
        Assert.Empty(seen);
    });

    [Fact]
    public Task GoToDriveHandsOffWithoutFlippingTheCardClosed() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        harness.Press(Key.Space);
        List<bool> seen = [];
        harness.Model.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(harness.Model.IsQuickAccessOpen))
                seen.Add(harness.Model.IsQuickAccessOpen);
        };

        harness.Model.Palette.PaletteSearchText = "Go to Drive…";
        harness.Settle();
        harness.Press(Key.Enter);

        Assert.True(harness.Model.Palette.IsPaletteOpen);
        Assert.All(
            harness.Model.Palette.Items,
            entry => Assert.StartsWith(CommandDef.DriveIdPrefix, entry.Command.Def.Id));
        Assert.Empty(seen);
    });

    [Fact]
    public Task ClosingThePaletteAfterARegularCommandStillFlipsTheCard() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        harness.Press(Key.Space);
        List<bool> seen = [];
        harness.Model.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(harness.Model.IsQuickAccessOpen))
                seen.Add(harness.Model.IsQuickAccessOpen);
        };

        harness.Model.Palette.PaletteSearchText = "Show/Hide Hidden";
        harness.Settle();
        harness.Press(Key.Enter);

        Assert.False(harness.Model.Palette.IsPaletteOpen);
        Assert.Equal([false], seen);
    });

    [Fact]
    public Task OpenWithOnAFolderSaysItNeedsAFile() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        harness.Highlight("docs");

        RunOpenWith(harness);

        Assert.False(harness.Model.Palette.IsPaletteOpen);
        Assert.Equal("Open With needs a file highlighted.", harness.Model.StatusInfo);
    });

    [Fact]
    public Task TheEverydayPaletteNeverListsTheAppEntries() => OnUiThread(() =>
    {
        using WindowHarness harness = WindowHarness.Open(Fill);
        harness.Highlight("notes.txt");

        harness.Press(Key.Space);

        Assert.Contains(harness.Model.Palette.Items, entry => entry.Command.Def.Id == CommandDef.OpenWith.Id);
        Assert.DoesNotContain(
            harness.Model.Palette.Items,
            entry => CommandDef.IsTransient(entry.Command.Def.Id));
    });
}
