using Avalonia.Input;
using Rove.UI.Services;
using Xunit;

namespace Rove.UI.Tests;

public class KeymapConfigParsingTests
{
    [Fact]
    public void ReadsOneOverridePerMode()
    {
        KeymapLoad load = KeymapConfig.Parse("""
        {
          "browse":  { "ctrl+j": "content.move_down" },
          "palette": { "ctrl+k": "palette.move_up" }
        }
        """);

        Assert.Empty(load.Problems);
        Assert.Equal(2, load.Overrides.Count);
        Assert.Contains(load.Overrides, o =>
            o.Mode == Mode.Browse
            && o.Stroke == new KeyStroke(Key.J, KeyModifiers.Control)
            && o.Action == CommandDef.ContentMoveDown.Id);
    }

    [Fact]
    public void NullUnbindsTheKey()
    {
        KeymapLoad load = KeymapConfig.Parse("""{ "browse": { "d": null, "x": "" } }""");

        Assert.Empty(load.Problems);
        Assert.Equal(2, load.Overrides.Count);
        Assert.All(load.Overrides, o => Assert.Null(o.Action));
    }

    [Fact]
    public void CommentsAndTrailingCommasAreAllowed()
    {
        KeymapLoad load = KeymapConfig.Parse("""
        {
          // move down with ctrl+j as well
          "browse": { "ctrl+j": "content.move_down", }
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
          "nosuchmode": { "j": "content.move_down" },
          "browse": { "nosuchkey": "content.move_down", "z": "content.no_such_command" }
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
