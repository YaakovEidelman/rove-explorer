using System.Diagnostics;
using Rove.Core.Protocol;

namespace Rove.Core.Services;

internal sealed class ProgressTicker
{
    private static readonly TimeSpan _interval = TimeSpan.FromMilliseconds(60);

    private readonly IProgress<FileOpProgress>? _sink;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private TimeSpan? _lastSent;

    public ProgressTicker(IProgress<FileOpProgress>? sink)
    {
        _sink = sink;
    }

    public void Report(string verb, int completed, int total, string currentItem, bool important = false)
    {
        if (_sink is null)
            return;
        TimeSpan now = _clock.Elapsed;
        if (!important && _lastSent is { } last && now - last < _interval)
            return;
        _lastSent = now;
        _sink.Report(new FileOpProgress(verb, completed, total, currentItem));
    }
}
