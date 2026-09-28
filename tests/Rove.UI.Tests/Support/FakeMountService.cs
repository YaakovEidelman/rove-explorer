using Rove.Core.Protocol;
using Rove.Core.Services;

namespace Rove.UI.Tests;

internal sealed class FakeMountService : IMountService
{
    public List<MountEntry> Entries { get; } = [];

    public List<string> Calls { get; } = [];

    public string MountedPath { get; set; } = "/run/media/me/STICK";

    public Func<string, IMountPrompter, Task<CommandResult<string>>>? OnConnect { get; set; }

    public event Action? Changed;

    public void StartWatching() => Calls.Add("watch");

    public Task<MountEntry[]> ListAsync(CancellationToken ct) => Task.FromResult(Entries.ToArray());

    public Task<CommandResult<string>> MountAsync(MountEntry entry, IMountPrompter prompter, CancellationToken ct)
    {
        Calls.Add("mount " + entry.Name);
        return Task.FromResult(CommandResult<string>.Ok(MountedPath));
    }

    public Task<CommandResult<string>> ConnectAsync(string address, IMountPrompter prompter, CancellationToken ct)
    {
        Calls.Add("connect " + address);
        return OnConnect?.Invoke(address, prompter) ?? Task.FromResult(CommandResult<string>.Ok(MountedPath));
    }

    public Task<CommandResult<bool>> UnmountAsync(MountEntry entry, bool eject, CancellationToken ct)
    {
        Calls.Add((eject ? "eject " : "unmount ") + entry.Name);
        return Task.FromResult(CommandResult<bool>.Ok(true));
    }

    public void RaiseChanged() => Changed?.Invoke();

    public void Dispose()
    {
    }
}
