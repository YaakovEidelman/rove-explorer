using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Rove.UI.Services;
using Rove.UI.ViewModels;

namespace Rove.UI.Views;

public partial class PickerWindow : Window
{
    private readonly KeyRepeatPacer _repeat;

    public bool ExitOnClose { get; set; } = true;

    public PickerWindow()
    {
        InitializeComponent();
        if (LoadIcon() is { } icon)
            Icon = icon;
        _repeat = new(() => WaylandKeyRepeat.RateOf(this), () => Environment.TickCount64);
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, (_, e) => _repeat.Release(e.Key), RoutingStrategies.Tunnel);
        Deactivated += (_, _) => _repeat.Reset();
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
            e.Handled = _repeat.Press(new(e.Key, e.KeyModifiers), () => vm.HandleKey(e.Key, e.KeyModifiers));
    }
}
