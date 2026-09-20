using Rove.Core.Protocol;

namespace Rove.Core.Services;

public interface IAdminSession : IDisposable
{
    bool IsRunning { get; }

    Task<CommandResult<FolderItem[]>> ReadDirectoryAsync(string path);
}

public static class AdminSessionChooser
{
    public static IAdminSession? CreateForHost() =>
        OperatingSystem.IsLinux() ? LinuxAdminSession.ForPkexec() : null;
}
