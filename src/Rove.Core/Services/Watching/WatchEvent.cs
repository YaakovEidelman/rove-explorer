using Rove.Core.Protocol;

namespace Rove.Core.Services;

public abstract record WatchEvent
{
    public sealed record Upserted(FolderItem Item) : WatchEvent;

    public sealed record Deleted(string Path) : WatchEvent;

    public sealed record Renamed(string OldPath, FolderItem Item) : WatchEvent;
}
