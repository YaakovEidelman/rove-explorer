using Avalonia.Media;
using Rove.Core.Protocol;

namespace Rove.UI.Services;

public interface IIconCache
{
    Task<IImage?> GetIconAsync(FolderItem item, int size);

    bool TryGetIcon(FolderItem item, int size, out IImage? icon);
}
