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
