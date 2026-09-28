namespace Rove.Core.Services;

public sealed class MountChangeSignal : IDisposable
{
    private static readonly TimeSpan _settle = TimeSpan.FromMilliseconds(400);

    private readonly object _gate = new();
    private CancellationTokenSource? _pending;
    private bool _disposed;

    public event Action? Fired;

    public void Raise()
    {
        CancellationTokenSource next = new();
        lock (_gate)
        {
            if (_disposed)
                return;
            _pending?.Cancel();
            _pending = next;
        }
        _ = Task.Delay(_settle, next.Token).ContinueWith(
            t =>
            {
                if (!t.IsCanceled)
                    Fired?.Invoke();
            },
            CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _pending?.Cancel();
        }
    }
}
