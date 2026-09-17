using Avalonia.Input;
using Rove.UI.Services;
using Xunit;

namespace Rove.UI.Tests;

public class KeyStrokeParsingTests
{
    [Theory]
    [InlineData("j", Key.J, KeyModifiers.None)]
    [InlineData("J", Key.J, KeyModifiers.None)]
    [InlineData("ctrl+n", Key.N, KeyModifiers.Control)]
    [InlineData("Control+Shift+P", Key.P, KeyModifiers.Control | KeyModifiers.Shift)]
    [InlineData("alt+enter", Key.Enter, KeyModifiers.Alt)]
    [InlineData("space", Key.Space, KeyModifiers.None)]
    [InlineData("esc", Key.Escape, KeyModifiers.None)]
    [InlineData("/", Key.OemQuestion, KeyModifiers.None)]
    [InlineData("0", Key.D0, KeyModifiers.None)]
    [InlineData("f5", Key.F5, KeyModifiers.None)]
    [InlineData("backspace", Key.Back, KeyModifiers.None)]
    public void ParsesTheKeyNamesTheAppItselfPrints(string text, Key key, KeyModifiers modifiers)
    {
        Assert.True(KeymapConfig.TryParseStroke(text, out KeyStroke stroke), text);
        Assert.Equal(new KeyStroke(key, modifiers), stroke);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("hyper+j")]
    [InlineData("ctrl+notakey")]
    public void RefusesNonsense(string text)
    {
        Assert.False(KeymapConfig.TryParseStroke(text, out _));
    }
}

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

public class CommandRegistryOverrideTests
{
    [Fact]
    public void AConfigBindingBeatsTheDefaultOnTheSameKey()
    {
        KeymapLoad load = new(
            [new(Mode.Browse, new KeyStroke(Key.J, KeyModifiers.None), CommandDef.ContentMoveUp.Id)],
            []);
        CommandRegistry registry = new(load);

        string? ran = null;
        registry.Register(CommandDef.ContentMoveUp, () => ran = "up");
        registry.Register(CommandDef.ContentMoveDown, () => ran = "down");

        Assert.True(registry.TryExecute(Mode.Browse, new KeyStroke(Key.J, KeyModifiers.None)));
        Assert.Equal("up", ran);
    }

    [Fact]
    public void ANullActionRemovesTheDefaultBinding()
    {
        KeymapLoad load = new([new(Mode.Browse, new KeyStroke(Key.J, KeyModifiers.None), null)], []);
        CommandRegistry registry = new(load);
        registry.Register(CommandDef.ContentMoveDown, () => { });

        Assert.False(registry.TryExecute(Mode.Browse, new KeyStroke(Key.J, KeyModifiers.None)));
    }

    [Fact]
    public void KeysTheConfigDoesNotMentionKeepTheirDefaults()
    {
        KeymapLoad load = new(
            [new(Mode.Browse, new KeyStroke(Key.J, KeyModifiers.None), CommandDef.ContentMoveUp.Id)],
            []);
        CommandRegistry registry = new(load);

        bool ranDown = false;
        registry.Register(CommandDef.ContentMoveUp, () => { });
        registry.Register(CommandDef.ContentMoveDown, () => ranDown = true);

        Assert.True(registry.TryExecute(Mode.Browse, new KeyStroke(Key.N, KeyModifiers.Control)));
        Assert.True(ranDown);
    }

    [Fact]
    public void NoConfigAtAllLeavesTheDefaultsExactlyAsTheyWere()
    {
        CommandRegistry registry = new();
        bool ran = false;
        registry.Register(CommandDef.ContentMoveDown, () => ran = true);

        Assert.True(registry.TryExecute(Mode.Browse, new KeyStroke(Key.J, KeyModifiers.None)));
        Assert.True(ran);
    }
}

public class CommandRemapTests
{
    private static CommandRegistry WithBrowseBinding(Key key, KeyModifiers modifiers, string action) =>
        new(new KeymapLoad([new(Mode.Browse, new KeyStroke(key, modifiers), action)], []));

    [Fact]
    public void GivingACommandANewKeyTakesItsOldKeyAway()
    {
        CommandRegistry registry = WithBrowseBinding(Key.J, KeyModifiers.Control, CommandDef.ContentMoveDown.Id);

        int ran = 0;
        registry.Register(CommandDef.ContentMoveDown, () => ran++);

        Assert.True(registry.TryExecute(Mode.Browse, new KeyStroke(Key.J, KeyModifiers.Control)));
        Assert.False(registry.TryExecute(Mode.Browse, new KeyStroke(Key.J, KeyModifiers.None)));
        Assert.False(registry.TryExecute(Mode.Browse, new KeyStroke(Key.N, KeyModifiers.Control)));
        Assert.Equal(1, ran);
    }

    [Fact]
    public void MovingACommandLeavesEveryOtherCommandAlone()
    {
        CommandRegistry registry = WithBrowseBinding(Key.J, KeyModifiers.Control, CommandDef.ContentMoveDown.Id);

        bool ranUp = false;
        registry.Register(CommandDef.ContentMoveDown, () => { });
        registry.Register(CommandDef.ContentMoveUp, () => ranUp = true);

        Assert.True(registry.TryExecute(Mode.Browse, new KeyStroke(Key.K, KeyModifiers.None)));
        Assert.True(ranUp);
    }

    [Fact]
    public void ACommandOnlyMovesInTheModeThatRebindsIt()
    {
        CommandRegistry registry = WithBrowseBinding(Key.J, KeyModifiers.Control, CommandDef.ContentMoveDown.Id);

        bool ran = false;
        registry.Register(CommandDef.ContentMoveDown, () => ran = true);

        Assert.True(registry.TryExecute(Mode.LocalSearch, new KeyStroke(Key.Down, KeyModifiers.None)));
        Assert.True(ran);
    }

    [Fact]
    public void KeepingADefaultKeyMeansWritingItDownToo()
    {
        CommandRegistry registry = new(new KeymapLoad(
            [
                new(Mode.Browse, new KeyStroke(Key.J, KeyModifiers.Control), CommandDef.ContentMoveDown.Id),
                new(Mode.Browse, new KeyStroke(Key.J, KeyModifiers.None), CommandDef.ContentMoveDown.Id),
            ],
            []));

        registry.Register(CommandDef.ContentMoveDown, () => { });

        Assert.True(registry.TryExecute(Mode.Browse, new KeyStroke(Key.J, KeyModifiers.Control)));
        Assert.True(registry.TryExecute(Mode.Browse, new KeyStroke(Key.J, KeyModifiers.None)));
    }
}
