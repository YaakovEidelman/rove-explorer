using Avalonia.Controls;

namespace Rove.UI.Views;

public partial class PaletteView : UserControl
{
    public PaletteView()
    {
        InitializeComponent();
        palette_list.SelectionChanged += (_, _) =>
        {
            if (palette_list.SelectedIndex >= 0)
                palette_list.ScrollIntoView(palette_list.SelectedIndex);
        };
    }
}
