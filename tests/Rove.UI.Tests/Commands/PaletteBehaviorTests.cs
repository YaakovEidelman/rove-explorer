using Rove.UI.Services;
using Rove.UI.ViewModels;
using Xunit;

namespace Rove.UI.Tests;

public class PaletteBehaviorTests
{
    [Fact]
    public void EmptyPaletteGroupsByCategoryThenOrder_NotAlphabetical()
    {
        CommandRegistry registry = new();
        registry.Register(new CommandDef("app.z", "Zeta App Thing", CommandKind.User, 0, CommandCategory.App), () => { });
        registry.Register(new CommandDef("file.b", "Beta File Thing", CommandKind.User, 1, CommandCategory.File), () => { });
        registry.Register(new CommandDef("file.a", "Alpha File Thing", CommandKind.User, 0, CommandCategory.File), () => { });
        PaletteViewModel palette = new(registry);

        palette.TogglePalette();

        Assert.Equal(
            ["File", "Alpha File Thing", "Beta File Thing", "App", "Zeta App Thing"],
            palette.Items.Select(row => row.Header ?? row.Entry!.Command.Def.Title));
    }

    [Fact]
    public void CommandsThatCannotRunHereAreGreyedOutNotHidden()
    {
        CommandRegistry registry = new();
        bool canRun = false;
        registry.Register(
            new CommandDef("content.restore_trashed_test", "Put Back", CommandKind.User, 0, CommandCategory.File),
            () => { },
            canRun: () => canRun);
        PaletteViewModel palette = new(registry);

        palette.TogglePalette();
        Assert.Equal(
            ["File", "Put Back"],
            palette.Items.Select(row => row.Header ?? row.Entry!.Command.Def.Title));
        PaletteRow row = palette.Items[1];
        Assert.False(row.IsSelectable);
        Assert.False(row.Entry!.IsAvailable);
        Assert.Equal(-1, palette.SelectedIndex);

        canRun = true;
        palette.Refresh();
        Assert.True(palette.Items[1].IsSelectable);
        Assert.True(palette.Items[1].Entry!.IsAvailable);
    }

    [Fact]
    public void RunningACommandAddsItToRecentAndPersistsAcrossReopen()
    {
        string settingsFile = Path.Combine(Path.GetTempPath(), "rove-palette-recent-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            SettingsStore settings = new(settingsFile);
            CommandRegistry registry = new();
            bool ran = false;
            registry.Register(new CommandDef("file.rename_test", "Rename Item", CommandKind.User, 0, CommandCategory.File),
                () => ran = true);
            PaletteViewModel palette = new(registry, settings);

            palette.TogglePalette();
            palette.ExecuteOption();
            Assert.True(ran);

            palette.TogglePalette();
            Assert.Equal("Recent", palette.Items[0].Header);
            Assert.Equal("file.rename_test", palette.Items[1].Entry!.Command.Def.Id);

            SettingsStore reloaded = new(settingsFile);
            CommandRegistry registry2 = new();
            registry2.Register(new CommandDef("file.rename_test", "Rename Item", CommandKind.User, 0, CommandCategory.File),
                () => { });
            PaletteViewModel palette2 = new(registry2, reloaded);
            palette2.TogglePalette();

            Assert.Equal("Recent", palette2.Items[0].Header);
            Assert.Equal("file.rename_test", palette2.Items[1].Entry!.Command.Def.Id);
        }
        finally
        {
            File.Delete(settingsFile);
        }
    }

    [Fact]
    public void ARecentCommandThatCanNoLongerRunIsGreyedOutNotHidden()
    {
        CommandRegistry registry = new();
        bool canRun = true;
        registry.Register(
            new CommandDef("content.restore_trashed_test", "Put Back", CommandKind.User, 0, CommandCategory.File),
            () => { },
            canRun: () => canRun);
        PaletteViewModel palette = new(registry);
        palette.TogglePalette();
        palette.ExecuteOption();
        Assert.False(palette.IsPaletteOpen);

        canRun = false;
        palette.TogglePalette();

        Assert.Equal("Recent", palette.Items[0].Header);
        PaletteRow row = palette.Items[1];
        Assert.Equal("content.restore_trashed_test", row.Entry!.Command.Def.Id);
        Assert.False(row.IsSelectable);
    }
}
