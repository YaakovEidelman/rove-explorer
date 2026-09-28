using Rove.Core.Protocol;
using Rove.Core.Services;

namespace Rove.Core.Tests;

internal sealed class FakeMountService(string name, params MountEntry[] entries) : IMountService
{
    public List<string> Calls { get; } = [];

    public event Action? Changed;

    public List<MountTool> Missing { get; } = [];

    IReadOnlyList<MountTool> IMountService.Missing => Missing;

    public void StartWatching() => Calls.Add("watch");

    public Task<MountEntry[]> ListAsync(CancellationToken ct) => Task.FromResult(entries);

    public Task<CommandResult<string>> MountAsync(MountEntry entry, IMountPrompter prompter, CancellationToken ct)
    {
        Calls.Add("mount " + entry.Name);
        return Task.FromResult(CommandResult<string>.Ok(name));
    }

    public Task<CommandResult<string>> ConnectAsync(string address, IMountPrompter prompter, CancellationToken ct)
    {
        Calls.Add("connect " + address);
        return Task.FromResult(CommandResult<string>.Ok(name));
    }

    public Task<CommandResult<bool>> UnmountAsync(MountEntry entry, bool eject, CancellationToken ct)
    {
        Calls.Add("unmount " + entry.Name);
        return Task.FromResult(CommandResult<bool>.Ok(true));
    }

    public Task<DriveFormat[]> FormatsAsync(CancellationToken ct) => Task.FromResult(DriveFormat.All);

    public Task<CommandResult<string>> FormatAsync(
        MountEntry entry, DriveFormat format, string name, CancellationToken ct)
    {
        Calls.Add("format " + entry.Name);
        return Task.FromResult(CommandResult<string>.Ok(""));
    }

    public void RaiseChanged() => Changed?.Invoke();

    public void Dispose() => Calls.Add("dispose");
}
