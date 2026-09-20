using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Rove.UI.Behaviors;

public class FocusOnTrue
{
    public static readonly AttachedProperty<bool> FocusProperty =
        AvaloniaProperty.RegisterAttached<FocusOnTrue, Control, bool>("Focus", false);

    public static readonly AttachedProperty<bool> SelectAllProperty =
        AvaloniaProperty.RegisterAttached<FocusOnTrue, Control, bool>("SelectAll", true);

    static FocusOnTrue()
    {
        FocusProperty.Changed.AddClassHandler<Control>(OnFocusChanged);
    }

    private static void OnFocusChanged(Control control, AvaloniaPropertyChangedEventArgs args)
    {
        if (control is TextBox textBox && args.NewValue is true)
        {
            bool selectAll = GetSelectAll(control);
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                textBox.Focus();
                textBox.CaretIndex = textBox.Text?.Length ?? 0;
                if (selectAll)
                    textBox.SelectAll();
            }, DispatcherPriority.Render);
        }
    }

    public static bool GetFocus(AvaloniaObject element) => element.GetValue(FocusProperty);

    public static void SetFocus(AvaloniaObject element, bool value) => element.SetValue(FocusProperty, value);

    public static bool GetSelectAll(AvaloniaObject element) => element.GetValue(SelectAllProperty);

    public static void SetSelectAll(AvaloniaObject element, bool value) => element.SetValue(SelectAllProperty, value);
}
