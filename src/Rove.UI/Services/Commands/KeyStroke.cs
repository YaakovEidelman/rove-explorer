using Avalonia.Input;
using System.Globalization;

namespace Rove.UI.Services;

public readonly record struct KeyStroke(Key Key, KeyModifiers Modifiers)
{
    public string Display()
    {
        string key = Key switch
        {
            Key.OemQuestion => "/",
            Key.Space => "Space",
            Key.Enter => "Enter",
            Key.Escape => "Esc",
            Key.Back => "Backspace",
            Key.Tab => "Tab",
            Key.Left => "←",
            Key.Right => "→",
            Key.Up => "↑",
            Key.Down => "↓",
            >= Key.D0 and <= Key.D9 => ((int)(Key - Key.D0)).ToString(CultureInfo.InvariantCulture),
            _ => Key.ToString(),
        };
        List<string> parts = [];
        if (Modifiers.HasFlag(KeyModifiers.Control)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(KeyModifiers.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(KeyModifiers.Shift)) parts.Add("Shift");
        parts.Add(key);
        return string.Join("+", parts);
    }
}
