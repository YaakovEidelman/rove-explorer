using Rove.Core.Protocol;

namespace Rove.Core.Services;

public class NoIconFetcher : IIconFetcher
{
    public Task<byte[]?> GetIconAsync(FolderItem item, int size)
    {
        return Task.FromResult((byte[]?)null);
    }
}
