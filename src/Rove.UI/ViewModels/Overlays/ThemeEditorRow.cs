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

    public ThemeEditorRow(ThemeColorField field, string label, string value, bool isColor, IBrush? swatch)
    {
        Field = field;
        _label = label;
        _value = value;
        _isColor = isColor;
        _swatch = swatch;
    }
}
