using Avalonia.Input;
using Rove.UI.Services;
using Xunit;

namespace Rove.UI.Tests;

public class KeymapConfigParsingTests
{
    [Fact]
    public void PlainCommandAppliesToItsDefaultMode()
    {
        KeymapLoad load = KeymapConfig.Parse("""{ "content.move_down": "ctrl+j" }""");

        Assert.Empty(load.Problems);
        Assert.Contains(load.Overrides, o =>
            o.Mode == Mode.Browse
            && o.Stroke == new KeyStroke(Key.J, KeyModifiers.Control)
            && o.Action == CommandDef.ContentMoveDown.Id);
    }

    [Fact]
    public void PlainCommandAppliesToEveryModeItAlreadyLivesIn()
    {
        KeymapLoad load = KeymapConfig.Parse("""{ "content.move_up": "ctrl+shift+k" }""");

        Assert.Empty(load.Problems);
        KeyStroke stroke = new(Key.K, KeyModifiers.Control | KeyModifiers.Shift);
        Assert.Contains(load.Overrides, o => o.Mode == Mode.Browse && o.Stroke == stroke && o.Action == CommandDef.ContentMoveUp.Id);
        Assert.Contains(load.Overrides, o => o.Mode == Mode.LocalSearch && o.Stroke == stroke && o.Action == CommandDef.ContentMoveUp.Id);
    }

    [Fact]
    public void ArrayGivesACommandSeveralKeys()
    {
        KeymapLoad load = KeymapConfig.Parse("""{ "content.move_down": ["ctrl+shift+j", "alt+j"] }""");

        Assert.Empty(load.Problems);
        Assert.All(load.Overrides, o => Assert.Equal(CommandDef.ContentMoveDown.Id, o.Action));
        Assert.Contains(load.Overrides, o => o.Mode == Mode.Browse && o.Stroke == new KeyStroke(Key.J, KeyModifiers.Control | KeyModifiers.Shift));
        Assert.Contains(load.Overrides, o => o.Mode == Mode.Browse && o.Stroke == new KeyStroke(Key.J, KeyModifiers.Alt));
    }

    [Fact]
    public void NullUnbindsEveryDefaultKeyForTheCommand()
    {
        KeymapLoad load = KeymapConfig.Parse("""{ "content.move_up": null }""");

        Assert.Empty(load.Problems);
        Assert.Equal(4, load.Overrides.Count);
        Assert.All(load.Overrides, o => Assert.Null(o.Action));
        Assert.Contains(load.Overrides, o => o.Mode == Mode.Browse);
        Assert.Contains(load.Overrides, o => o.Mode == Mode.LocalSearch);
    }

    [Fact]
    public void EmptyStringAlsoUnbinds()
    {
        KeymapLoad load = KeymapConfig.Parse("""{ "content.delete": "" }""");

        Assert.Empty(load.Problems);
        Assert.Single(load.Overrides);
        Assert.Null(load.Overrides[0].Action);
    }

    [Fact]
    public void ModeFormRestrictsToOneMode()
    {
        KeymapLoad load = KeymapConfig.Parse(
            """{ "content.move_up": { "mode": "browse", "key": "ctrl+shift+k" } }""");

        Assert.Empty(load.Problems);
        Assert.Single(load.Overrides);
        Assert.Equal(Mode.Browse, load.Overrides[0].Mode);
    }

    [Fact]
    public void ModeFormLetsAKeylessCommandPickAMode()
    {
        KeymapLoad load = KeymapConfig.Parse(
            """{ "app.toggle_theme": { "mode": "browse", "key": "ctrl+shift+t" } }""");

        Assert.Empty(load.Problems);
        Assert.Single(load.Overrides);
        Assert.Equal(Mode.Browse, load.Overrides[0].Mode);
        Assert.Equal(CommandDef.ToggleTheme.Id, load.Overrides[0].Action);
    }

    [Fact]
    public void PlainKeylessCommandWithoutModeIsReported()
    {
        KeymapLoad load = KeymapConfig.Parse("""{ "app.toggle_theme": "ctrl+shift+t" }""");

        Assert.Empty(load.Overrides);
        Assert.Single(load.Problems);
    }

    [Fact]
    public void CommentsAndTrailingCommasAreAllowed()
    {
        KeymapLoad load = KeymapConfig.Parse("""
        {
          // move down with ctrl+j as well
          "palette.execute": "ctrl+j",
        }
        """);

        Assert.Empty(load.Problems);
        Assert.Single(load.Overrides);
    }

    [Fact]
    public void UnknownNamesAreReportedAndSkippedRatherThanThrown()
    {
        KeymapLoad load = KeymapConfig.Parse("""
        {
          "content.no_such_command": "j",
          "content.move_down": "nosuchkey",
          "content.move_up": { "mode": "nosuchmode", "key": "j" }
        }
        """);

        Assert.Empty(load.Overrides);
        Assert.Equal(3, load.Problems.Count);
        Assert.NotNull(load.Summary);
    }

    [Fact]
    public void BrokenJsonIsReportedNotThrown()
    {
        KeymapLoad load = KeymapConfig.Parse("{ not json ");

        Assert.Empty(load.Overrides);
        Assert.Single(load.Problems);
    }

    [Fact]
    public void MissingFileMeansPlainDefaults()
    {
        KeymapLoad load = KeymapConfig.Load(Path.Combine(Path.GetTempPath(), "rove-no-such-file.json"));

        Assert.Empty(load.Overrides);
        Assert.Empty(load.Problems);
        Assert.Null(load.Summary);
    }

    [Fact]
    public void EveryCommandTheDefaultsBindIsANameAConfigCanUse()
    {
        HashSet<string> known = [.. KeymapConfig.KnownCommandIds()];

        foreach (KommandShortcut[] bindings in KeymapDefaults.DefaultModeBindings.Values)
            foreach (KommandShortcut binding in bindings)
                Assert.Contains(binding.Action, known);
    }
}
