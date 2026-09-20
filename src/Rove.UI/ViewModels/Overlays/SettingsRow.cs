using CommunityToolkit.Mvvm.ComponentModel;

namespace Rove.UI.ViewModels;

public partial class SettingsRow : ObservableObject
{
    [ObservableProperty]
    private string _label;

    [ObservableProperty]
    private string _value;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSection))]
    private string? _section;

    [ObservableProperty]
    private bool _isToggle;

    [ObservableProperty]
    private bool _isOn;

    public bool HasSection => Section is not null;

    public SettingsRow(string label, string value, string? section = null, bool isToggle = false, bool isOn = false)
    {
        _label = label;
        _value = value;
        _section = section;
        _isToggle = isToggle;
        _isOn = isOn;
    }
}
