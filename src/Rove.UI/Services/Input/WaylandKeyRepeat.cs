using Avalonia.Controls;
using System.Runtime.CompilerServices;

namespace Rove.UI.Services;

public static class WaylandKeyRepeat
{
    private const string ImplType = "Avalonia.Wayland.WindowBaseImpl";
    private const string SinkType = "Avalonia.Wayland.WindowBaseImpl+Sink";

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "get_CurrentSink")]
    [return: UnsafeAccessorType(SinkType + ", Avalonia.Wayland")]
    internal static extern object? Sink([UnsafeAccessorType(ImplType + ", Avalonia.Wayland")] object impl);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_keyRepeatRate")]
    internal static extern ref int PerSecond([UnsafeAccessorType(SinkType + ", Avalonia.Wayland")] object sink);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_keyRepeatDelay")]
    internal static extern ref int DelayMs([UnsafeAccessorType(SinkType + ", Avalonia.Wayland")] object sink);

    public static KeyRepeatRate? RateOf(TopLevel window)
    {
        try
        {
            if (window.PlatformImpl is not { } impl || !Extends(impl.GetType(), ImplType))
                return null;
            if (Sink(impl) is not { } sink || !Extends(sink.GetType(), SinkType))
                return null;
            return KeyRepeatRate.FromPerSecond(PerSecond(sink), DelayMs(sink));
        }
        catch (Exception ex) when (ex is MissingMemberException or NotSupportedException or TypeLoadException)
        {
            return null;
        }
    }

    internal static bool Extends(Type? type, string fullName)
    {
        for (; type is not null; type = type.BaseType)
        {
            if (type.FullName == fullName)
                return true;
        }
        return false;
    }
}
