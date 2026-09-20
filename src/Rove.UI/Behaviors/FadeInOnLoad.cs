using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Rove.UI.Behaviors;

public class FadeInOnLoad
{
    public static readonly AttachedProperty<bool> EnabledProperty =
        AvaloniaProperty.RegisterAttached<FadeInOnLoad, Control, bool>("Enabled", false);

    static FadeInOnLoad()
    {
        EnabledProperty.Changed.AddClassHandler<Control>(OnEnabledChanged);
    }

    private static void OnEnabledChanged(Control control, AvaloniaPropertyChangedEventArgs args)
    {
        if (args.NewValue is not true)
            return;

        control.Classes.Remove("entered");
        control.AttachedToVisualTree += OnAttached;
    }

    private static void OnAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is not Control control)
            return;
        control.AttachedToVisualTree -= OnAttached;
        Dispatcher.UIThread.Post(() => control.Classes.Add("entered"), DispatcherPriority.Loaded);
    }

    public static bool GetEnabled(AvaloniaObject element) => element.GetValue(EnabledProperty);

    public static void SetEnabled(AvaloniaObject element, bool value) => element.SetValue(EnabledProperty, value);
}
