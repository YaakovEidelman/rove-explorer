using Avalonia.Controls;

namespace Rove.UI.Views;

public partial class GlobalSearchView : UserControl
{
    public GlobalSearchView()
    {
        InitializeComponent();
        search_list.SelectionChanged += (_, _) =>
        {
            if (search_list.SelectedIndex >= 0)
                search_list.ScrollIntoView(search_list.SelectedIndex);
        };
    }
}
