using Rove.Core.Endpoints;
using Rove.Core.Services;

namespace Rove.Core;

public class RoveCore : IDisposable
{
    private readonly List<FileWatchService> _watchers = [];

    public Actions Actions { get; } = new();
    public GlobalSearchService Search { get; } = new();

    public IAdminSession? Admin { get; }

    public RoveCore() : this(AdminSessionChooser.CreateForHost())
    {
    }

    public RoveCore(IAdminSession? admin)
    {
        Admin = admin;
    }

    public FileWatchService NewWatcher()
    {
        FileWatchService watcher = new();
        lock (_watchers)
            _watchers.Add(watcher);
        return watcher;
    }

    public void ReleaseWatcher(FileWatchService watcher)
    {
        lock (_watchers)
            _watchers.Remove(watcher);
        watcher.Dispose();
    }

    public void Dispose()
    {
        Admin?.Dispose();
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
