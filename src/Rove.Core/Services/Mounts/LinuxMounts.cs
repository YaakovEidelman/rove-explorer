using Rove.Core.Protocol;

namespace Rove.Core.Services;

public sealed class LinuxMounts(IMountService drives, IMountService? remote) : IMountService
{
    public event Action? Changed
    {
        add
        {
            drives.Changed += value;
            if (remote is not null)
                remote.Changed += value;
        }
        remove
        {
            drives.Changed -= value;
            if (remote is not null)
                remote.Changed -= value;
        }
    }

    public void StartWatching()
    {
        drives.StartWatching();
        remote?.StartWatching();
    }

    public async Task<MountEntry[]> ListAsync(CancellationToken ct)
    {
        Task<MountEntry[]> local = drives.ListAsync(ct);
        MountEntry[] far = remote is null ? [] : await remote.ListAsync(ct).ConfigureAwait(false);
        return [.. await local.ConfigureAwait(false), .. far.Where(e => !IsDrive(e))];
    }

    public Task<CommandResult<string>> MountAsync(MountEntry entry, IMountPrompter prompter, CancellationToken ct) =>
        For(entry)?.MountAsync(entry, prompter, ct) ?? NoRemote<string>();

    public Task<CommandResult<string>> ConnectAsync(string address, IMountPrompter prompter, CancellationToken ct) =>
        remote?.ConnectAsync(address, prompter, ct) ?? NoRemote<string>();

    public Task<CommandResult<bool>> UnmountAsync(MountEntry entry, bool eject, CancellationToken ct) =>
        For(entry)?.UnmountAsync(entry, eject, ct) ?? NoRemote<bool>();

    public void Dispose()
    {
        drives.Dispose();
        remote?.Dispose();
    }

    private IMountService? For(MountEntry entry) => IsDrive(entry) ? drives : remote;

    private static bool IsDrive(MountEntry entry) => entry.Kind is MountKind.Disk or MountKind.Removable;

    private static Task<CommandResult<T>> NoRemote<T>() =>
        Task.FromResult(CommandResult<T>.Fail(
            "no_remote", "Servers and phones need gio (part of GLib), which isn't available here."));
}
