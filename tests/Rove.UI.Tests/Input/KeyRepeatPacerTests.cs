using Avalonia.Input;
using Rove.UI.Services;
using Xunit;

namespace Rove.UI.Tests;

public class KeyRepeatPacerTests
{
    private static readonly KeyStroke J = new(Key.J, KeyModifiers.None);
    private static readonly KeyStroke K = new(Key.K, KeyModifiers.None);
    private static readonly KeyRepeatRate Rate = new(DelayMs: 250, IntervalMs: 25);

    private long _now;
    private int _runs;

    private KeyRepeatPacer Pacer(KeyRepeatRate? rate) => new(() => rate, () => _now);

    private bool Run()
    {
        _runs++;
        return true;
    }

    private void PressAt(KeyRepeatPacer pacer, long at, KeyStroke? stroke = null)
    {
        _now = at;
        pacer.Press(stroke ?? J, Run);
    }

    [Fact]
    public void AFirstPressRunsOnce()
    {
        KeyRepeatPacer pacer = Pacer(Rate);

        PressAt(pacer, 0);

        Assert.Equal(1, _runs);
    }

    [Fact]
    public void RepeatsThatArriveOnTimeRunOnceEach()
    {
        KeyRepeatPacer pacer = Pacer(Rate);

        PressAt(pacer, 0);
        PressAt(pacer, 250);
        PressAt(pacer, 275);
        PressAt(pacer, 300);

        Assert.Equal(4, _runs);
    }

    [Fact]
    public void LateRepeatsCatchUpToTheConfiguredRate()
    {
        KeyRepeatPacer pacer = Pacer(Rate);

        PressAt(pacer, 0);
        PressAt(pacer, 260);
        PressAt(pacer, 310);
        PressAt(pacer, 360);
        PressAt(pacer, 410);

        Assert.Equal(1 + 7, _runs);
    }

    [Fact]
    public void ACatchUpNeverRunsMoreThanTheCap()
    {
        KeyRepeatPacer pacer = Pacer(Rate);

        PressAt(pacer, 0);
        PressAt(pacer, 250);
        PressAt(pacer, 520);

        Assert.Equal(2 + KeyRepeatPacer.MaxStepsPerEvent, _runs);
    }

    [Fact]
    public void AReleasedKeyStartsOverOnTheNextPress()
    {
        KeyRepeatPacer pacer = Pacer(Rate);

        PressAt(pacer, 0);
        pacer.Release(Key.J);
        PressAt(pacer, 400);

        Assert.Equal(2, _runs);
    }

    [Fact]
    public void AGapLongerThanTheDelayCountsAsANewPress()
    {
        KeyRepeatPacer pacer = Pacer(Rate);

        PressAt(pacer, 0);
        PressAt(pacer, 1000);

        Assert.Equal(2, _runs);
    }

    [Fact]
    public void ADifferentKeyStartsOver()
    {
        KeyRepeatPacer pacer = Pacer(Rate);

        PressAt(pacer, 0);
        PressAt(pacer, 250);
        PressAt(pacer, 400, K);

        Assert.Equal(3, _runs);
    }

    [Fact]
    public void AnUnknownRateRunsEveryPressOnce()
    {
        KeyRepeatPacer pacer = Pacer(null);

        PressAt(pacer, 0);
        PressAt(pacer, 250);
        PressAt(pacer, 400);

        Assert.Equal(3, _runs);
    }

    [Fact]
    public void AnUnhandledKeyStopsTheCatchUp()
    {
        KeyRepeatPacer pacer = Pacer(Rate);
        int calls = 0;
        bool Unhandled()
        {
            calls++;
            return false;
        }

        _now = 0;
        pacer.Press(J, Unhandled);
        _now = 350;
        bool handled = pacer.Press(J, Unhandled);

        Assert.False(handled);
        Assert.Equal(2, calls);
    }
}
