using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Rove.UI.ViewModels;
using System;
using System.IO;

namespace Rove.UI.Views;

public partial class PickerWindow : Window
{
    public bool ExitOnClose { get; set; } = true;

    public PickerWindow()
    {
        InitializeComponent();
        if (LoadIcon() is { } icon)
            Icon = icon;
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        completion_list.SelectionChanged += (_, _) =>
        {
            if (completion_list.SelectedIndex >= 0)
                completion_list.ScrollIntoView(completion_list.SelectedIndex);
        };
        Closed += (_, _) =>
        {
            if (ExitOnClose)
                Environment.Exit(PickerWindowViewModel.CancelExitCode);
        };
    }

    private static WindowIcon? LoadIcon()
    {
        string name = OperatingSystem.IsWindows() ? "rove.ico" : "png/rove-256.png";
        using Stream? stream = typeof(PickerWindow).Assembly.GetManifestResourceStream(name);
        return stream is null ? null : new WindowIcon(stream);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is PickerWindowViewModel vm)
            e.Handled = vm.HandleKey(e.Key, e.KeyModifiers);
    }
}
