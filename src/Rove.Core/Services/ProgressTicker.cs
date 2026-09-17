using System.Diagnostics;
using Rove.Core.Protocol;

namespace Rove.Core.Services;

/// <summary>
/// Rate-limits progress reports. A recursive copy touches thousands of files
/// a second and the UI can't paint that fast, so anything arriving inside the
/// interval is dropped — except a report marked important, which always gets
/// through so the last state the user sees is the true one.
/// </summary>
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
