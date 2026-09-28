using Rove.Core.Protocol;

namespace Rove.Core.Services;

public interface IMountService : IDisposable
{
    event Action? Changed;

    void StartWatching();

    Task<MountEntry[]> ListAsync(CancellationToken ct);

    Task<CommandResult<string>> MountAsync(MountEntry entry, IMountPrompter prompter, CancellationToken ct);

    Task<CommandResult<string>> ConnectAsync(string address, IMountPrompter prompter, CancellationToken ct);

    Task<CommandResult<bool>> UnmountAsync(MountEntry entry, bool eject, CancellationToken ct);
}
