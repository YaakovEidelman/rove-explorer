using Rove.Core.Protocol;

namespace Rove.Core.Services;

public abstract record WatchEvent
{
    public sealed record Upserted(FolderItem Item) : WatchEvent;

    public sealed record Deleted(string Path) : WatchEvent;

    public sealed record Renamed(string OldPath, FolderItem Item) : WatchEvent;
}

public class FileWatchService : IDisposable
{
    private static readonly TimeSpan DefaultCoalesceWindow = TimeSpan.FromMilliseconds(75);

    private readonly FileSystemWatcher _watcher = new()
    {
        NotifyFilter = NotifyFilters.Attributes
            | NotifyFilters.CreationTime
            | NotifyFilters.DirectoryName
            | NotifyFilters.FileName
            | NotifyFilters.LastWrite
            | NotifyFilters.Size,
        InternalBufferSize = 64 * 1024,
    };

    private abstract record PendingChange
    {
        public sealed record Upsert(string Path) : PendingChange;

        public sealed record Delete(string Path) : PendingChange;

        public sealed record Rename(string OldPath, string NewPath) : PendingChange;
    }

    private readonly TimeSpan _coalesceWindow;
    private readonly Lock _gate = new();
    private List<PendingChange> _pending = [];
    private Timer? _flushTimer;
    private Action<IReadOnlyList<WatchEvent>>? _apply;

    public FileWatchService(TimeSpan? coalesceWindow = null)
    {
        _coalesceWindow = coalesceWindow ?? DefaultCoalesceWindow;
    }

    public void ChangePath(string path)
    {
        _watcher.EnableRaisingEvents = false;
        _watcher.Path = path;
        _watcher.EnableRaisingEvents = true;
    }

    public void Pause() => _watcher.EnableRaisingEvents = false;

    public void Resume()
    {
        if (!string.IsNullOrEmpty(_watcher.Path))
            _watcher.EnableRaisingEvents = true;
    }

    private static FolderItem? TryDescribe(string path)
    {
        try
        {
            if (Directory.Exists(path))
                return FolderItem.From(new DirectoryInfo(path));
            if (File.Exists(path))
                return FolderItem.From(new FileInfo(path));
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
        }
        return null;
    }

    public void Subscribe(Action<IReadOnlyList<WatchEvent>> apply, Action resync)
    {
        _apply = apply;
        _watcher.Changed += (_, e) => Enqueue(new PendingChange.Upsert(e.FullPath));
        _watcher.Created += (_, e) => Enqueue(new PendingChange.Upsert(e.FullPath));
        _watcher.Deleted += (_, e) => Enqueue(new PendingChange.Delete(e.FullPath));
        _watcher.Renamed += (_, e) => Enqueue(new PendingChange.Rename(e.OldFullPath, e.FullPath));
        _watcher.Error += (_, _) =>
        {
            lock (_gate)
            {
                _pending = [];
                _flushTimer?.Dispose();
                _flushTimer = null;
            }
            resync();
        };
    }

    private void Enqueue(PendingChange e)
    {
        lock (_gate)
        {
            _pending.Add(e);
            _flushTimer ??= new Timer(_ => Flush(), null, _coalesceWindow, Timeout.InfiniteTimeSpan);
        }
    }

    private void Flush()
    {
        List<PendingChange> batch;
        lock (_gate)
        {
            batch = _pending;
            _pending = [];
            _flushTimer?.Dispose();
            _flushTimer = null;
        }
        if (batch.Count == 0)
            return;
        _apply?.Invoke([.. batch.Select(Resolve)]);
    }

    private static WatchEvent Resolve(PendingChange change) => change switch
    {
        PendingChange.Upsert u => TryDescribe(u.Path) is { } item
            ? new WatchEvent.Upserted(item)
            : new WatchEvent.Deleted(u.Path),
        PendingChange.Delete d => new WatchEvent.Deleted(d.Path),
        PendingChange.Rename r => TryDescribe(r.NewPath) is { } item
            ? new WatchEvent.Renamed(r.OldPath, item)
            : new WatchEvent.Deleted(r.OldPath),
        _ => throw new ArgumentOutOfRangeException(nameof(change)),
    };

    public void Dispose()
    {
        lock (_gate)
        {
            _flushTimer?.Dispose();
            _flushTimer = null;
        }
        _watcher.Dispose();
        GC.SuppressFinalize(this);
    }
}
