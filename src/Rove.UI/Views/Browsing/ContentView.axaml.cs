using Avalonia.Controls;
using Avalonia.Threading;
using Rove.UI.ViewModels;
using System.ComponentModel;

namespace Rove.UI.Views;

public partial class ContentView : UserControl
{
    private ContentViewModel? _boundViewModel;

    public ContentView()
    {
        InitializeComponent();
        content_list.SelectionChanged += (_, _) => ScrollHighlightIntoView(content_list);
        icon_list.SelectionChanged += (_, _) => ScrollHighlightIntoView(icon_list);
        icon_list.LayoutUpdated += (_, _) => UpdateColumnsPerRow();
        DataContextChanged += (_, _) => BindViewModel();
    }

    private static void ScrollHighlightIntoView(ListBox list)
    {
        if (!list.IsVisible || list.SelectedIndex < 0)
            return;
        Dispatcher.UIThread.Post(() =>
        {
            if (list.SelectedIndex >= 0)
                list.ScrollIntoView(list.SelectedIndex);
        }, DispatcherPriority.Loaded);
    }

    private void BindViewModel()
    {
        if (_boundViewModel is not null)
            _boundViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _boundViewModel = DataContext as ContentViewModel;
        if (_boundViewModel is not null)
            _boundViewModel.PropertyChanged += OnViewModelPropertyChanged;
        UpdateColumnsPerRow();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ContentViewModel.ViewMode))
            UpdateColumnsPerRow();
    }

    private void UpdateColumnsPerRow()
    {
        if (_boundViewModel is not { } vm)
            return;
        vm.DirectoryListing.ListSelection.SetColumnsPerRow(vm.IsListView ? 1 : CountFirstRow());
    }

    private int CountFirstRow()
    {
        if (icon_list.ItemsPanelRoot is not { Children.Count: > 0 } panel)
            return 1;
        double firstTop = panel.Children[0].Bounds.Top;
        return panel.Children.Count(c => Math.Abs(c.Bounds.Top - firstTop) < 0.5);
    }
}
