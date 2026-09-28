using Rove.Core.Protocol;
using Tmds.DBus.Protocol;

namespace Rove.Core.Services;

public sealed partial class UDisksMounts(string home) : IMountService
{
    private const int FormatSettleTries = 20;
    private static readonly TimeSpan FormatSettleDelay = TimeSpan.FromMilliseconds(100);

    private readonly MountChangeSignal _change = new();
    private IDisposable? _watch;
    private bool _watching;
    private bool _disposed;

    public event Action? Changed
    {
        add => _change.Fired += value;
        remove => _change.Fired -= value;
    }

    public async Task<MountEntry[]> ListAsync(CancellationToken ct)
    {
        if (await ConnectionAsync().ConfigureAwait(false) is not { } connection)
            return [];
        try
        {
            return UDisksMountList.Build(await ObjectsAsync(connection).ConfigureAwait(false), home);
        }
        catch (DBusErrorReplyException)
        {
            return [];
        }
        catch (DBusExceptionBase)
        {
            Forget(connection);
            return [];
        }
    }

    public Task<CommandResult<string>> MountAsync(MountEntry entry, IMountPrompter prompter, CancellationToken ct)
    {
        if (entry.LocalPath is { } mounted)
            return Task.FromResult(CommandResult<string>.Ok(mounted));

        return WithBlockAsync(entry, "mount_failed", $"Couldn't mount {entry.Name}.", async (connection, objects, block) =>
        {
            string target = block.Path;
            if (block.IsEncrypted)
            {
                if ((objects.CleartextOf(block)?.Path
                        ?? await AskToUnlockAsync(connection, block, entry.Name, prompter).ConfigureAwait(false))
                    is not { } cleartext)
                    return CommandResult<string>.Fail("cancelled", $"Left {entry.Name} locked.");
                target = cleartext;
            }
            try
            {
                return CommandResult<string>.Ok(await MountAsync(connection, target).ConfigureAwait(false));
            }
            catch (DBusErrorReplyException ex) when (ex.ErrorName == AlreadyMounted)
            {
                UDisksObjects now = await ObjectsAsync(connection).ConfigureAwait(false);
                if (now.Blocks.FirstOrDefault(b => b.Path == target)?.MountPoints.FirstOrDefault() is { } path)
                    return CommandResult<string>.Ok(path);
                throw;
            }
        });
    }

    public Task<CommandResult<string>> ConnectAsync(string address, IMountPrompter prompter, CancellationToken ct) =>
        Task.FromResult(CommandResult<string>.Fail("no_remote", "UDisks only opens drives, not servers."));

    public Task<CommandResult<bool>> UnmountAsync(MountEntry entry, bool eject, CancellationToken ct) =>
        WithBlockAsync(entry, "unmount_failed", $"Couldn't unmount {entry.Name}.", async (connection, objects, block) =>
        {
            UDisksDrive? drive = objects.DriveOf(block);
            IEnumerable<UDisksBlock> release = eject && drive is not null
                ? objects.Blocks.Where(b => b.CryptoBackingDevice is null && objects.DriveOf(b) == drive)
                : [block];
            foreach (UDisksBlock each in release)
                await ReleaseAsync(connection, objects, each).ConfigureAwait(false);
            if (eject && drive is not null)
                await EjectAsync(connection, drive).ConfigureAwait(false);
            return CommandResult<bool>.Ok(true);
        });

    public async Task<DriveFormat[]> FormatsAsync(CancellationToken ct)
    {
        if (await ConnectionAsync().ConfigureAwait(false) is not { } connection)
            return [];
        List<DriveFormat> usable = [];
        foreach (DriveFormat format in DriveFormat.All)
        {
            try
            {
                if (await CanFormatAsync(connection, format.Type).ConfigureAwait(false))
                    usable.Add(format);
            }
            catch (DBusErrorReplyException)
            {
                usable.Add(format);
            }
            catch (DBusExceptionBase)
            {
                Forget(connection);
                return [];
            }
        }
        return [.. usable];
    }

    public Task<CommandResult<string>> FormatAsync(
        MountEntry entry, DriveFormat format, string name, CancellationToken ct) =>
        WithBlockAsync(entry, "format_failed", $"Couldn't format {entry.Name}.", async (connection, objects, block) =>
        {
            await ReleaseAsync(connection, objects, block).ConfigureAwait(false);
            await FormatAsync(connection, block.Path, format.Type, name).ConfigureAwait(false);
            for (int tries = 0; tries < FormatSettleTries; tries++)
            {
                UDisksBlock? now = (await ObjectsAsync(connection).ConfigureAwait(false)).BlockOf(block.Device);
                if (now is { Uuid.Length: > 0 } && now.Uuid != block.Uuid)
                    return CommandResult<string>.Ok(now.Uuid);
                await Task.Delay(FormatSettleDelay, CancellationToken.None).ConfigureAwait(false);
            }
            return CommandResult<string>.Ok("");
        });

    public void StartWatching()
    {
        if (_watching)
            return;
        _watching = true;
        _ = WatchAsync();
    }

    public void Dispose()
    {
        _disposed = true;
        _change.Dispose();
        _watch?.Dispose();
        _connection?.Dispose();
        _connection = null;
    }

    private async Task WatchAsync()
    {
        if (await ConnectionAsync().ConfigureAwait(false) is not { } connection)
            return;
        MatchRule rule = new() { Type = MessageType.Signal, Sender = Service, PathNamespace = Root };
        try
        {
            _watch = await connection.AddMatchAsync(
                rule,
                static (message, _) => IsMountChange(message),
                (Notification<bool> changed) =>
                {
                    if (changed.HasValue && changed.Value)
                        _change.Raise();
                },
                emitOnCapturedContext: false,
                ObserverFlags.None,
                null).ConfigureAwait(false);
        }
        catch (DBusExceptionBase)
        {
            _watching = false;
        }
    }

    private async Task<CommandResult<T>> WithBlockAsync<T>(
        MountEntry entry,
        string reason,
        string fallback,
        Func<DBusConnection, UDisksObjects, UDisksBlock, Task<CommandResult<T>>> work)
    {
        if (await ConnectionAsync().ConfigureAwait(false) is not { } connection)
            return CommandResult<T>.Fail(reason, "Couldn't reach UDisks, the system disk service.");
        try
        {
            UDisksObjects objects = await ObjectsAsync(connection).ConfigureAwait(false);
            return entry.Device is { } device && objects.BlockOf(device) is { } block
                ? await work(connection, objects, block).ConfigureAwait(false)
                : CommandResult<T>.Fail("not_found", $"{entry.Name} isn't plugged in anymore.");
        }
        catch (DBusErrorReplyException ex)
        {
            return CommandResult<T>.Fail(reason, ErrorText(ex, fallback));
        }
        catch (DBusExceptionBase)
        {
            Forget(connection);
            return CommandResult<T>.Fail(reason, fallback);
        }
    }

    private static async Task<string?> AskToUnlockAsync(
        DBusConnection connection, UDisksBlock block, string name, IMountPrompter prompter)
    {
        string ask = $"Type the passphrase to unlock {name}.";
        string message = ask;
        while (await prompter.AskTextAsync(message, "Passphrase", secret: true, suggested: null).ConfigureAwait(false)
            is { } passphrase)
        {
            try
            {
                return await UnlockAsync(connection, block.Path, passphrase).ConfigureAwait(false);
            }
            catch (DBusErrorReplyException ex) when (!ex.ErrorName.StartsWith(NotAuthorized, StringComparison.Ordinal))
            {
                message = $"{GioMounts.RetryNote}\n{ask}";
            }
        }
        return null;
    }

    private static async Task ReleaseAsync(DBusConnection connection, UDisksObjects objects, UDisksBlock block)
    {
        UDisksBlock? cleartext = block.IsEncrypted ? objects.CleartextOf(block) : null;
        UDisksBlock? filesystem = block.IsEncrypted ? cleartext : block;
        if (filesystem is { HasFilesystem: true, MountPoints.Length: > 0 })
            await CallAsync(connection, filesystem.Path, UDisksObjects.FilesystemInterface, "Unmount")
                .ConfigureAwait(false);
        if (cleartext is not null)
            await CallAsync(connection, block.Path, UDisksObjects.EncryptedInterface, "Lock").ConfigureAwait(false);
    }

    private static async Task EjectAsync(DBusConnection connection, UDisksDrive drive)
    {
        if (!drive.Ejectable)
        {
            if (drive.CanPowerOff)
                await CallAsync(connection, drive.Path, UDisksObjects.DriveInterface, "PowerOff").ConfigureAwait(false);
            return;
        }

        await CallAsync(connection, drive.Path, UDisksObjects.DriveInterface, "Eject").ConfigureAwait(false);
        if (!drive.CanPowerOff)
            return;
        try
        {
            await CallAsync(connection, drive.Path, UDisksObjects.DriveInterface, "PowerOff").ConfigureAwait(false);
        }
        catch (DBusErrorReplyException)
        {
        }
    }
}
