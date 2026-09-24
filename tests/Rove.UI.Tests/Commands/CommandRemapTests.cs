using Avalonia.Input;
using Rove.UI.Services;
using Xunit;

namespace Rove.UI.Tests;

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
