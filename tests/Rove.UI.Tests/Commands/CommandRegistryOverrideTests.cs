using Avalonia.Input;
using Rove.UI.Services;
using Xunit;

namespace Rove.UI.Tests;

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
