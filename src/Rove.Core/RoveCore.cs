using Rove.Core.Endpoints;
using Rove.Core.Services;

namespace Rove.Core;

/// <summary>
/// Facade the UI talks to. Everything runs in-process; the Dispatcher in
/// Protocol/ exposes the same verbs over a JSON envelope for a future
/// out-of-process transport.
/// </summary>
public class RoveCore : IDisposable
{
    /// <summary>Watchers handed out and not yet given back, so shutdown can close them.</summary>
    private readonly List<FileWatchService> _watchers = [];

    public Actions Actions { get; } = new();
    public GlobalSearchService Search { get; } = new();

    /// <summary>
    /// A watcher of a folder's own. Every folder view has one, because a
    /// watcher follows exactly one directory and two views are rarely
    /// looking at the same one.
    /// </summary>
    public FileWatchService NewWatcher()
    {
        FileWatchService watcher = new();
        lock (_watchers)
            _watchers.Add(watcher);
        return watcher;
    }

    /// <summary>Closes a watcher whose view has gone, and forgets it.</summary>
    public void ReleaseWatcher(FileWatchService watcher)
    {
        lock (_watchers)
            _watchers.Remove(watcher);
        watcher.Dispose();
    }

    public void Dispose()
    {
        FileWatchService[] open;
        lock (_watchers)
        {
            open = [.. _watchers];
            _watchers.Clear();
        }
        foreach (FileWatchService watcher in open)
            watcher.Dispose();
    }
}
