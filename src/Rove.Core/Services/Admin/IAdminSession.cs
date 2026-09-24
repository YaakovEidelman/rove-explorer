using Rove.Core.Protocol;

namespace Rove.Core.Services;

public interface IAdminSession : IDisposable
{
    bool IsRunning { get; }

    Task<CommandResult<FolderItem[]>> ReadDirectoryAsync(string path);

    Task<CommandResult<string>> CopyToTempAsync(string path, long maxBytes, CancellationToken ct = default);

    void Discard(string copy);
}
