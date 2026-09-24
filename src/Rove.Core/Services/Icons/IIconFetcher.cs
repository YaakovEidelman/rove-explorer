using Rove.Core.Protocol;

namespace Rove.Core.Services;

public interface IIconFetcher
{
    Task<byte[]?> GetIconAsync(FolderItem item, int size);
}
