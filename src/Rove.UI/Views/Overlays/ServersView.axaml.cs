using Avalonia.Controls;

namespace Rove.UI.Views;

public partial class ServersView : UserControl
{
    public ServersView()
    {
        InitializeComponent();
        server_list.SelectionChanged += (_, _) =>
        {
            if (server_list.SelectedIndex >= 0)
                server_list.ScrollIntoView(server_list.SelectedIndex);
        };
    }
}
