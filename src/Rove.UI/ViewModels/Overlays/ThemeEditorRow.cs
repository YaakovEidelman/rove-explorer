using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Rove.UI.ViewModels;

public partial class ThemeEditorRow : ObservableObject
{
    public ThemeColorField Field { get; }

    [ObservableProperty]
    private string _label;

    [ObservableProperty]
    private string _value;

    [ObservableProperty]
    private bool _isColor;

    [ObservableProperty]
    private IBrush? _swatch;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private string _editText = string.Empty;

    [ObservableProperty]
    private int _red;

    [ObservableProperty]
    private int _green;

    [ObservableProperty]
    private int _blue;

    private bool _syncing;

    public ThemeEditorRow(ThemeColorField field, string label, string value, bool isColor, IBrush? swatch)
    {
        Field = field;
        _label = label;
        _value = value;
        _isColor = isColor;
        _swatch = swatch;
    }

    public void BeginEditing(string hexDigits)
    {
        _syncing = true;
        EditText = hexDigits;
        _syncing = false;
        SyncFromHex(hexDigits);
        IsEditing = true;
    }

    partial void OnEditTextChanged(string value)
    {
        if (_syncing)
            return;
        SyncFromHex(value);
    }

    partial void OnRedChanged(int value) => SyncFromRgb();

    partial void OnGreenChanged(int value) => SyncFromRgb();

    partial void OnBlueChanged(int value) => SyncFromRgb();

    private void SyncFromHex(string hexDigits)
    {
        if (!TryParseHexDigits(hexDigits, out byte r, out byte g, out byte b))
            return;

        _syncing = true;
        Red = r;
        Green = g;
        Blue = b;
        _syncing = false;
        Swatch = new SolidColorBrush(Color.FromRgb(r, g, b));
    }

    private void SyncFromRgb()
    {
        if (_syncing)
            return;

        _syncing = true;
        EditText = $"{(byte)Red:X2}{(byte)Green:X2}{(byte)Blue:X2}";
        _syncing = false;
        Swatch = new SolidColorBrush(Color.FromRgb((byte)Red, (byte)Green, (byte)Blue));
    }

    private static bool TryParseHexDigits(string hexDigits, out byte r, out byte g, out byte b)
    {
        r = g = b = 0;
        if (hexDigits.Length != 6)
            return false;

        try
        {
            r = Convert.ToByte(hexDigits[..2], 16);
            g = Convert.ToByte(hexDigits[2..4], 16);
            b = Convert.ToByte(hexDigits[4..6], 16);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
