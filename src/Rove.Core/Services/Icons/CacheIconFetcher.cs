using System.Collections.Concurrent;
using Rove.Core.Protocol;

namespace Rove.Core.Services;

public class CacheIconFetcher : IIconFetcher
{
    private readonly IIconFetcher _inner;
    private readonly ConcurrentDictionary<string, Task<byte[]?>> _cache = new();

    public CacheIconFetcher(IIconFetcher inner)
    {
        _inner = inner;
    }

    public Task<byte[]?> GetIconAsync(FolderItem item, int size)
    {
        string key = item.IsDirectory
            ? "<dir>"
            : string.IsNullOrEmpty(item.Extension) ? "<no-ext>" : item.Extension;
        key = $"{key}|{size}";
        return _cache.GetOrAdd(key, _ => FetchAndKeepOnlyIfGood(key, item, size));
    }

    private async Task<byte[]?> FetchAndKeepOnlyIfGood(string key, FolderItem item, int size)
    {
        try
        {
            byte[]? img = await _inner.GetIconAsync(item, size);
            if (img is null)
                _cache.TryRemove(key, out _);
            return img;
        }
        catch
        {
            _cache.TryRemove(key, out _);
            throw;
        }
    }
}
