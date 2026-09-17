using Avalonia.Controls;

namespace Rove.UI.Views;

public partial class BookmarksView : UserControl
{
    public BookmarksView()
    {
        InitializeComponent();
        bookmark_list.SelectionChanged += (_, _) =>
        {
            if (bookmark_list.SelectedIndex >= 0)
                bookmark_list.ScrollIntoView(bookmark_list.SelectedIndex);
        };
    }
}
