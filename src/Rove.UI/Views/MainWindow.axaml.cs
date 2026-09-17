using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Rove.UI.ViewModels;
using System;
using System.IO;

namespace Rove.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        if (LoadIcon() is { } icon)
            Icon = icon;
        if (LoadBrandIcon() is { } brand)
            brand_icon.Source = brand;
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        completion_list.SelectionChanged += (_, _) =>
        {
            if (completion_list.SelectedIndex >= 0)
                completion_list.ScrollIntoView(completion_list.SelectedIndex);
        };
        tabs_scroll.LayoutUpdated += (_, _) => UpdateTabOverflow();
    }

    private void UpdateTabOverflow()
    {
        int count = tabs_items.ItemCount;
        double available = tabs_scroll.Viewport.Width;
        if (count == 0 || available <= 0)
            return;

        const double spacing = 4;
        double newTabWidth = tabnew_button.Bounds.Width;

        int visible = FitCount(count, available - spacing - newTabWidth, spacing);
        bool overflowing = visible < count;
        if (overflowing)
        {
            double reserved = spacing + newTabWidth + spacing + tabs_overflow_toggle.Bounds.Width;
            visible = FitCount(count, available - reserved, spacing);
        }

        for (int i = 0; i < count; i++)
        {
            if (tabs_items.ContainerFromIndex(i) is not Control container)
                continue;
            bool shouldShow = i < visible;
            if (container.IsVisible != shouldShow)
                container.IsVisible = shouldShow;
        }

        if (tabs_overflow_toggle.IsVisible != overflowing)
            tabs_overflow_toggle.IsVisible = overflowing;
        if (tabs_end_marker.IsVisible != overflowing)
            tabs_end_marker.IsVisible = overflowing;
    }

    private int FitCount(int count, double budget, double spacing)
    {
        double used = 0;
        int visible = 0;
        for (int i = 0; i < count; i++)
        {
            if (tabs_items.ContainerFromIndex(i) is not Control container)
                break;
            double next = used + (visible > 0 ? spacing : 0) + container.Bounds.Width;
            if (next > budget)
                break;
            used = next;
            visible++;
        }
        return visible;
    }

    /// <summary>Picking a tab from the overflow list closes the list too.</summary>
    private void OnOverflowItemClick(object? sender, RoutedEventArgs e) =>
        tabs_overflow_toggle.IsChecked = false;

    private void OnTabDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: FolderTab tab })
            tab.BeginRename();
    }

    /// <summary>
    /// The icon rides in the assembly as a plain resource, not an Avalonia
    /// one: the same bytes are what the installer writes into the desktop's
    /// icon theme, where there is no Avalonia to ask for them.
    /// </summary>
    private static WindowIcon? LoadIcon()
    {
        string name = OperatingSystem.IsWindows() ? "rove.ico" : "png/rove-256.png";
        using Stream? stream = typeof(MainWindow).Assembly.GetManifestResourceStream(name);
        return stream is null ? null : new WindowIcon(stream);
    }

    private static Bitmap? LoadBrandIcon()
    {
        using Stream? stream = typeof(MainWindow).Assembly.GetManifestResourceStream("png/rove-32.png");
        return stream is null ? null : new Bitmap(stream);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            e.Handled = vm.HandleKey(e.Key, e.KeyModifiers);
    }
}
