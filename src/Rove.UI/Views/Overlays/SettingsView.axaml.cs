using Avalonia.Controls;

namespace Rove.UI.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
        settings_list.SelectionChanged += (_, _) =>
        {
            if (settings_list.SelectedIndex >= 0)
                settings_list.ScrollIntoView(settings_list.SelectedIndex);
        };
        theme_list.SelectionChanged += (_, _) =>
        {
            if (theme_list.SelectedIndex >= 0)
                theme_list.ScrollIntoView(theme_list.SelectedIndex);
        };
    }
}
