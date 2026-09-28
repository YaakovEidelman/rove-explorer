namespace Rove.UI.Services;

public readonly record struct KeyRepeatRate(int DelayMs, int IntervalMs)
{
    public static KeyRepeatRate? FromPerSecond(int perSecond, int delayMs) =>
        perSecond > 0 ? new(delayMs, Math.Max(1, 1000 / perSecond)) : null;

    public long RepeatsBy(long heldMs) =>
        heldMs < DelayMs ? 0 : (heldMs - DelayMs) / IntervalMs + 1;

    public long StaleAfterMs => DelayMs + IntervalMs;
}
