using CommunityToolkit.Mvvm.ComponentModel;

namespace Rove.UI.ViewModels;

public partial class SettingsRow : ObservableObject
{
    public SettingsRowKind Kind { get; }

    public string Section { get; }

    [ObservableProperty]
    private string _label;

    [ObservableProperty]
    private string _value;

    [ObservableProperty]
    private bool _isToggle;

    [ObservableProperty]
    private bool _isOn;

    public SettingsRow(
        SettingsRowKind kind, string label, string value, string section, bool isToggle = false, bool isOn = false)
    {
        Kind = kind;
        Section = section;
        _label = label;
        _value = value;
        _isToggle = isToggle;
        _isOn = isOn;
    }
}
