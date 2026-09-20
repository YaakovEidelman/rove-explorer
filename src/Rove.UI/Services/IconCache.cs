using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Svg.Skia;
using Rove.Core.Protocol;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;

namespace Rove.UI.Services;

public interface IIconCache
{
    Task<IImage?> GetIconAsync(FolderItem item, int size);

    bool TryGetIcon(FolderItem item, int size, out IImage? icon);
}

public class IconCache : IIconCache
{
    private readonly Func<GetIconArgs, Task<CommandResult<byte[]?>>> _fetch;
    private readonly ConcurrentDictionary<(bool, string, int), Task<IImage?>> _cache = [];

    public IconCache(Func<GetIconArgs, Task<CommandResult<byte[]?>>> fetch)
    {
        _fetch = fetch;
    }

    public bool TryGetIcon(FolderItem item, int size, out IImage? icon)
    {
        icon = null;
        if (!_cache.TryGetValue(Key(item, size), out Task<IImage?>? task) || !task.IsCompletedSuccessfully)
            return false;
        icon = task.Result;
        return icon is not null;
    }

    public async Task<IImage?> GetIconAsync(FolderItem item, int size)
    {
        (bool, string, int) key = Key(item, size);
        Task<IImage?> task = _cache.GetOrAdd(key, _ => ExtractIconAsync(item, size));
        IImage? result = await task;
        if (result is null)
            _cache.TryRemove(new(key, task));
        return result;
    }

    private static (bool, string, int) Key(FolderItem item, int size) =>
        (item.IsDirectory, item.Extension, size);

    private async Task<IImage?> ExtractIconAsync(FolderItem item, int size)
    {
        try
        {
            byte[]? b = (await _fetch(new GetIconArgs(item, size))).Data;
            if (b is not { Length: > 0 } bytes)
                return null;
            using MemoryStream ms = new(bytes);
            return LooksLikeSvg(bytes)
                ? new SvgImage { Source = SvgSource.LoadFromStream(ms) }
                : new Bitmap(ms);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static bool LooksLikeSvg(byte[] bytes)
    {
        foreach (byte b in bytes)
        {
            if (b is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n')
                continue;
            return b == (byte)'<';
        }
        return false;
    }
}
