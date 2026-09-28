using Avalonia.Input;

namespace Rove.UI.Services;

public sealed class KeyRepeatPacer(Func<KeyRepeatRate?> rate, Func<long> nowMs)
{
    public const int MaxStepsPerEvent = 4;

    private KeyStroke? _held;
    private KeyRepeatRate _rate;
    private long _downAt;
    private long _lastAt;
    private long _repeats;

    public bool Press(KeyStroke stroke, Func<bool> run)
    {
        long now = nowMs();
        if (_held != stroke || now - _lastAt > _rate.StaleAfterMs)
            return Begin(stroke, now, run);

        long due = _rate.RepeatsBy(now - _downAt);
        long steps = Math.Clamp(due - _repeats, 1, MaxStepsPerEvent);
        _repeats = Math.Max(_repeats + steps, due);

        bool handled = true;
        for (long i = 0; i < steps && handled; i++)
            handled = run();
        _lastAt = nowMs();
        return handled;
    }

    public void Release(Key key)
    {
        if (_held?.Key == key)
            _held = null;
    }

    public void Reset() => _held = null;

    private bool Begin(KeyStroke stroke, long now, Func<bool> run)
    {
        _held = null;
        if (rate() is not { } known)
            return run();

        _rate = known;
        _held = stroke;
        _downAt = now;
        _repeats = 0;
        bool handled = run();
        _lastAt = nowMs();
        return handled;
    }
}
