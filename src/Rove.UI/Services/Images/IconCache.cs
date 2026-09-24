using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Svg.Skia;
using Rove.Core.Protocol;
using System.Collections.Concurrent;

namespace Rove.UI.Services;

public class IconCache : IIconCache
{
    private readonly Func<GetIconArgs, Task<CommandResult<byte[]?>>> _fetch;
    private readonly IImagePreviewLoader _thumbnails;
    private readonly ConcurrentDictionary<(bool, string, int), Task<IImage?>> _cache = [];

    public IconCache(Func<GetIconArgs, Task<CommandResult<byte[]?>>> fetch, IImagePreviewLoader thumbnails)
    {
        _fetch = fetch;
        _thumbnails = thumbnails;
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

    private (bool, string, int) Key(FolderItem item, int size) =>
        IsThumbnailable(item)
            ? (false, item.FullPath, size)
            : (item.IsDirectory, item.Extension, size);

    private bool IsThumbnailable(FolderItem item) =>
        !item.IsDirectory && _thumbnails.CanPreview(item.Extension);

    private async Task<IImage?> ExtractIconAsync(FolderItem item, int size)
    {
        if (IsThumbnailable(item)
            && await _thumbnails.LoadAsync(item.FullPath, size, CancellationToken.None) is { } thumbnail)
            return thumbnail;

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
