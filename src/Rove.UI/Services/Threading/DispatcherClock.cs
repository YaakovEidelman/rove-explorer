using Avalonia.Controls.Platform;
using Avalonia.Threading;
using System.Runtime.CompilerServices;

namespace Rove.UI.Services;

public static class DispatcherClock
{
    private const long ToleranceMs = 1;

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_timeProvider")]
    internal static extern ref Func<long> TimeProvider(Dispatcher dispatcher);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "get_PlatformImpl")]
    [return: UnsafeAccessorType("Avalonia.Threading.IDispatcherImpl, Avalonia.Base")]
    internal static extern object? Impl(Dispatcher dispatcher);

    public static bool Align(Dispatcher dispatcher)
    {
        try
        {
            if (Impl(dispatcher) is not ManagedDispatcherImpl impl)
                return false;
            return Align(ref TimeProvider(dispatcher), () => impl.Now);
        }
        catch (Exception ex) when (ex is MissingMemberException or NotSupportedException)
        {
            return false;
        }
    }

    internal static bool Align(ref Func<long> provider, Func<long> platformNow)
    {
        if (Math.Abs(provider() - platformNow()) <= ToleranceMs)
            return false;
        provider = platformNow;
        return true;
    }
}
