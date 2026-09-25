using Avalonia.Threading;
using Rove.UI.Services;
using Xunit;

namespace Rove.UI.Tests;

public class DispatcherClockTests : HeadlessTest
{
    private const long Skew = 140;

    [Fact]
    public void AlignPutsASkewedClockBackOnThePlatformClock()
    {
        long platform = 1000;
        Func<long> platformNow = () => platform;
        Func<long> provider = () => platform + Skew;

        Assert.True(DispatcherClock.Align(ref provider, platformNow));

        Assert.Same(platformNow, provider);
        platform = 5000;
        Assert.Equal(5000, provider());
    }

    [Fact]
    public void AlignLeavesAnAlreadyAlignedClockAlone()
    {
        Func<long> platformNow = () => 1000;
        Func<long> provider = () => 1000;
        Func<long> original = provider;

        Assert.False(DispatcherClock.Align(ref provider, platformNow));

        Assert.Same(original, provider);
    }

    [Fact]
    public Task AvaloniaStillHasTheDispatcherFieldsTheFixReaches() => OnUiThread(() =>
    {
        Dispatcher dispatcher = Dispatcher.UIThread;

        Assert.NotNull(DispatcherClock.TimeProvider(dispatcher));
        Assert.NotNull(DispatcherClock.Impl(dispatcher));
    });
}
