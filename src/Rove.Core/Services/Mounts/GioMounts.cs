using Rove.Core.Protocol;
using System.ComponentModel;
using System.Diagnostics;

namespace Rove.Core.Services;

public sealed partial class GioMounts(string gio) : IMountService
{
    private static readonly TimeSpan _settle = TimeSpan.FromMilliseconds(400);
    private const string WatchUntilStdinCloses = "\"$0\" mount -o & watcher=$!; read -r _; kill $watcher";

    private readonly object _gate = new();
    private Process? _monitor;
    private CancellationTokenSource? _pendingChange;
    private bool _disposed;

    public event Action? Changed;

    public async Task<MountEntry[]> ListAsync(CancellationToken ct)
    {
        GioRun listed = await RunAsync(["mount", "-li"], prompter: null, ct).ConfigureAwait(false);
        if (listed.ExitCode != 0)
            return [];

        MountEntry[] entries = GioMountList.Parse(listed.Output);
        return await Task.WhenAll(entries.Select(async entry =>
            entry is { IsMounted: true, LocalPath: null }
                ? entry with { LocalPath = await LocalPathAsync(entry.MountUri!, ct).ConfigureAwait(false) }
                : entry)).ConfigureAwait(false);
    }

    public async Task<CommandResult<string>> MountAsync(MountEntry entry, IMountPrompter prompter, CancellationToken ct)
    {
        if (entry.IsMounted)
            return entry.LocalPath is { } path
                ? CommandResult<string>.Ok(path)
                : CommandResult<string>.Fail("no_local_path", $"{entry.Name} has no folder Rove can open.");

        string[] args = entry.Device is { } device ? ["mount", "-d", device]
            : entry.ActivationUri is { } uri ? ["mount", uri]
            : [];
        if (args.Length == 0)
            return CommandResult<string>.Fail("not_mountable", $"{entry.Name} can't be mounted.");

        GioRun run = await RunAsync(args, prompter, ct).ConfigureAwait(false);
        if (run.ExitCode != 0)
            return CommandResult<string>.Fail("mount_failed", run.Error($"Couldn't mount {entry.Name}."));

        MountEntry[] now = await ListAsync(ct).ConfigureAwait(false);
        return now.FirstOrDefault(e => e.IsMounted && e.SameVolume(entry))?.LocalPath is { } mounted
            ? CommandResult<string>.Ok(mounted)
            : CommandResult<string>.Fail("no_local_path", $"Mounted {entry.Name}, but couldn't find its folder.");
    }

    public async Task<CommandResult<string>> ConnectAsync(string address, IMountPrompter prompter, CancellationToken ct)
    {
        string target = address.Trim();
        if (!MountAddress.LooksRemote(target))
            return CommandResult<string>.Fail("bad_address", $"{target} isn't a server address, e.g. sftp://host/path.");

        bool named = MountAddress.HasUser(target);
        GioRun run = await RunAsync(named ? ["mount", target] : ["mount", "-a", target], prompter, ct)
            .ConfigureAwait(false);
        if (run.ExitCode != 0 && !run.Prompted && !named)
            run = await RunAsync(["mount", target], prompter, ct).ConfigureAwait(false);

        if (await LocalPathAsync(target, ct).ConfigureAwait(false) is { } path)
            return CommandResult<string>.Ok(path);
        return CommandResult<string>.Fail("connect_failed", run.Error($"Couldn't connect to {target}."));
    }

    public async Task<CommandResult<bool>> UnmountAsync(MountEntry entry, bool eject, CancellationToken ct)
    {
        if (entry.MountUri is not { } uri)
            return CommandResult<bool>.Fail("not_mounted", $"{entry.Name} isn't mounted.");

        GioRun run = await RunAsync(["mount", eject ? "-e" : "-u", uri], prompter: null, ct).ConfigureAwait(false);
        return run.ExitCode == 0
            ? CommandResult<bool>.Ok(true)
            : CommandResult<bool>.Fail("unmount_failed", run.Error($"Couldn't unmount {entry.Name}."));
    }

    public void StartWatching()
    {
        lock (_gate)
        {
            if (_monitor is not null || _disposed)
                return;
            try
            {
                ProcessStartInfo watch = Start(["-c", WatchUntilStdinCloses, gio]);
                watch.FileName = "/bin/sh";
                _monitor = Process.Start(watch);
            }
            catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
            {
                return;
            }
        }
        if (_monitor is { } monitor)
            _ = WatchAsync(monitor);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _pendingChange?.Cancel();
            if (_monitor is { } monitor)
            {
                try
                {
                    monitor.Kill(entireProcessTree: true);
                }
                catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
                {
                }
                monitor.Dispose();
                _monitor = null;
            }
        }
    }

    private async Task WatchAsync(Process monitor)
    {
        try
        {
            while (await monitor.StandardOutput.ReadLineAsync().ConfigureAwait(false) is { } line)
            {
                if (line.Length > 0 && !line.StartsWith(' '))
                    SignalChange();
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
        {
        }
    }

    private void SignalChange()
    {
        CancellationTokenSource next = new();
        lock (_gate)
        {
            if (_disposed)
                return;
            _pendingChange?.Cancel();
            _pendingChange = next;
        }
        _ = Task.Delay(_settle, next.Token).ContinueWith(
            t =>
            {
                if (!t.IsCanceled)
                    Changed?.Invoke();
            },
            CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
    }

    private async Task<string?> LocalPathAsync(string uri, CancellationToken ct)
    {
        GioRun info = await RunAsync(["info", "-a", "standard::name", uri], prompter: null, ct).ConfigureAwait(false);
        if (info.ExitCode != 0)
            return null;
        const string key = "local path: ";
        return info.Output.Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .FirstOrDefault(line => line.StartsWith(key, StringComparison.Ordinal))?[key.Length..];
    }

    private ProcessStartInfo Start(string[] args)
    {
        ProcessStartInfo info = new(gio)
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (string arg in args)
            info.ArgumentList.Add(arg);
        return info;
    }
}
