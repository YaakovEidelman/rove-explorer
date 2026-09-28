using Rove.Core.Protocol;

namespace Rove.Core.Services;

public interface IMountService : IDisposable
{
    event Action? Changed;

    IReadOnlyList<MountTool> Missing { get; }

    void StartWatching();

    Task<MountEntry[]> ListAsync(CancellationToken ct);

    Task<CommandResult<string>> MountAsync(MountEntry entry, IMountPrompter prompter, CancellationToken ct);

    Task<CommandResult<string>> ConnectAsync(string address, IMountPrompter prompter, CancellationToken ct);

    Task<CommandResult<bool>> UnmountAsync(MountEntry entry, bool eject, CancellationToken ct);

    Task<DriveFormat[]> FormatsAsync(CancellationToken ct);

    Task<CommandResult<string>> FormatAsync(MountEntry entry, DriveFormat format, string name, CancellationToken ct);
}
