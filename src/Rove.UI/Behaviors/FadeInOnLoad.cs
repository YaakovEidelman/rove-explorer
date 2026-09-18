using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Rove.UI.Behaviors;

/// <summary>
/// Adds the "entered" class one frame after a control attaches to the visual
/// tree — for a style that starts an element faded/scaled down and transitions
/// it to normal on ".entered", so newly added items (a new tab, say) ease in
/// instead of popping straight to their resting state. Flipping the class
/// immediately on attach would give the transition nothing to animate from,
/// since the attach and the class both land in the same layout pass.
/// </summary>
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
