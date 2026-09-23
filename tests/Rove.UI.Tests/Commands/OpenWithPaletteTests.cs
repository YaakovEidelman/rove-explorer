using Rove.UI.Services;
using Rove.UI.ViewModels;
using Xunit;

namespace Rove.UI.Tests;

public class OpenWithPaletteTests
{
    private static (PaletteViewModel Palette, CommandRegistry Registry) Setup()
    {
        CommandRegistry registry = new();
        registry.Register(new CommandDef("nav.drives", "Go to Drive…", CommandKind.User), () => { });
        registry.Register(
            new CommandDef(CommandDef.OpenWithIdPrefix + "/usr/share/applications/kate.desktop", "Open with Kate", CommandKind.User),
            () => { });
        return (new PaletteViewModel(registry), registry);
    }

    [Fact]
    public void TheAppPickerListsOnlyTheApps()
    {
        (PaletteViewModel palette, _) = Setup();

        palette.OpenScoped(CommandDef.OpenWithIdPrefix, "open with…");

        Assert.Equal(["Open with Kate"], palette.Items.Select(row => row.Entry!.Command.Def.Title));
    }

    [Fact]
    public void TheEverydayPaletteNeverListsTheApps()
    {
        (PaletteViewModel palette, _) = Setup();

        palette.TogglePalette();

        Assert.Equal(
            ["Go to Drive…"],
            palette.Items.Where(row => !row.IsHeader).Select(row => row.Entry!.Command.Def.Title));
    }

    [Fact]
    public void TheAppListKeepsTheOrderItWasGivenNotAlphabetical()
    {
        CommandRegistry registry = new();
        string[] names = ["Zed", "Alpha", "Mid"];
        for (int i = 0; i < names.Length; i++)
        {
            registry.Register(
                new CommandDef(CommandDef.OpenWithIdPrefix + names[i], $"Open with {names[i]}", CommandKind.User, i),
                () => { });
        }
        PaletteViewModel palette = new(registry);

        palette.OpenScoped(CommandDef.OpenWithIdPrefix, "open with…");

        Assert.Equal(
            ["Open with Zed", "Open with Alpha", "Open with Mid"],
            palette.Items.Select(row => row.Entry!.Command.Def.Title));
    }

    [Fact]
    public void PickingAnAppRunsItsCommand()
    {
        CommandRegistry registry = new();
        string? opened = null;
        registry.Register(
            new CommandDef(CommandDef.OpenWithIdPrefix + "kate", "Open with Kate", CommandKind.User),
            () => opened = "kate");
        PaletteViewModel palette = new(registry);
        palette.OpenScoped(CommandDef.OpenWithIdPrefix, "open with…");

        palette.ExecuteOption();

        Assert.Equal("kate", opened);
        Assert.False(palette.IsPaletteOpen);
    }
}
