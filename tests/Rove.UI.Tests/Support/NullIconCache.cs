using Avalonia.Media;
using Rove.Core.Protocol;
using Rove.UI.Services;

namespace Rove.UI.Tests;

internal sealed class NullIconCache : IIconCache
{
    public Task<IImage?> GetIconAsync(FolderItem item, int size) => Task.FromResult<IImage?>(null);

    public bool TryGetIcon(FolderItem item, int size, out IImage? icon)
    {
        icon = null;
        return true;
    }
}
