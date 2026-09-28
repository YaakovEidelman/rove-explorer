using Rove.Core.Protocol;
using Rove.Core.Services;
using Rove.UI.Services;

namespace Rove.UI.ViewModels;

public sealed class MountsViewModel
{
    private const string AddressHint =
        "Type a server address, like sftp://me@host/folder, smb://nas/share or ftp://host.";

    private readonly ICommandTarget _registry;
    private readonly IMountService _service;
    private readonly IMountPrompter _prompter;
    private readonly Func<string, Task> _navigate;
    private readonly Func<string> _currentDirectory;
    private bool _busy;

    public MountsViewModel(
        ICommandTarget registry,
        IMountService service,
        IMountPrompter prompter,
        Func<string, Task> navigate,
        Func<string> currentDirectory)
    {
        _registry = registry;
        _service = service;
        _prompter = prompter;
        _navigate = navigate;
        _currentDirectory = currentDirectory;
        _registry.Register(CommandDef.ConnectToServer, () => _ = ConnectToServerAsync());
    }

    public event Action<string>? ErrorRaised;

    public event Action<string>? InfoRaised;

    public void Start()
    {
        _service.Changed += () => _ = RefreshAsync();
        _service.StartWatching();
        _ = RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        MountEntry[] entries = await _service.ListAsync(CancellationToken.None).ConfigureAwait(false);
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => Show(entries));
    }

    public void Show(IEnumerable<MountEntry> entries)
    {
        HashSet<string> live = [];
        foreach (MountEntry entry in entries)
        {
            foreach ((string verb, string title, Func<Task> run) in Actions(entry))
            {
                string id = $"{CommandDef.MountIdPrefix}{verb}:{entry.Key}";
                live.Add(id);
                _registry.Register(
                    new CommandDef(id, title, CommandKind.User, Category: CommandCategory.Navigation,
                        Keywords: Keywords(entry.Kind)),
                    () => _ = run());
            }
        }

        foreach (string id in _registry.CommandIdsStartingWith(CommandDef.MountIdPrefix))
        {
            if (!live.Contains(id))
                _registry.Unregister(id);
        }
    }

    public Task ConnectAsync(string address) => RunAsync($"Connecting to {address.Trim()}…", async () =>
    {
        CommandResult<string> result = await _service.ConnectAsync(address, _prompter, CancellationToken.None);
        await OpenAsync(result);
    });

    private async Task ConnectToServerAsync()
    {
        string? address = await _prompter.AskTextAsync(AddressHint, "Address", secret: false, "sftp://");
        if (address is { Length: > 0 } && MountAddress.LooksRemote(address))
            await ConnectAsync(address);
        else if (address is { Length: > 0 })
            ErrorRaised?.Invoke($"{address} isn't a server address. {AddressHint}");
    }

    private IEnumerable<(string Verb, string Title, Func<Task> Run)> Actions(MountEntry entry)
    {
        string noun = Noun(entry.Kind);
        if (!entry.IsMounted)
        {
            if (entry.CanMount)
                yield return ("open", $"Open {noun} {entry.Name}", () => MountAsync(entry));
            yield break;
        }

        if (!entry.IsFileBacked && entry.LocalPath is { } path)
            yield return ("goto", $"Go to {noun} {entry.Name}", () => _navigate(path));

        if (entry.CanEject)
            yield return ("eject", $"Eject {noun} {entry.Name}", () => UnmountAsync(entry, eject: true));
        else if (entry.CanUnmount)
            yield return entry.Kind == MountKind.Network
                ? ("unmount", $"Disconnect {noun} {entry.Name}", () => UnmountAsync(entry, eject: false))
                : ("unmount", $"Unmount {noun} {entry.Name}", () => UnmountAsync(entry, eject: false));
    }

    private Task MountAsync(MountEntry entry) => RunAsync($"Opening {entry.Name}…", async () =>
    {
        CommandResult<string> result = await _service.MountAsync(entry, _prompter, CancellationToken.None);
        await OpenAsync(result);
    });

    private Task UnmountAsync(MountEntry entry, bool eject) =>
        RunAsync(eject ? $"Ejecting {entry.Name}…" : $"Unmounting {entry.Name}…", async () =>
        {
            if (entry.LocalPath is { } root && IsInside(_currentDirectory(), root))
                await _navigate(PathCompare.DefaultStartDirectory());

            CommandResult<bool> result = await _service.UnmountAsync(entry, eject, CancellationToken.None);
            if (!result.IsOk)
            {
                ErrorRaised?.Invoke(result.Message ?? $"Couldn't unmount {entry.Name}.");
                return;
            }
            InfoRaised?.Invoke(eject
                ? $"Ejected {entry.Name}. It's safe to remove."
                : entry.Kind == MountKind.Network ? $"Disconnected {entry.Name}." : $"Unmounted {entry.Name}.");
            await RefreshAsync();
        });

    private async Task OpenAsync(CommandResult<string> result)
    {
        if (!result.IsOk || result.Data is not { } path)
        {
            ErrorRaised?.Invoke(result.Message ?? "Couldn't open that location.");
            return;
        }
        InfoRaised?.Invoke(string.Empty);
        await _navigate(path);
        await RefreshAsync();
    }

    private async Task RunAsync(string working, Func<Task> work)
    {
        if (_busy)
        {
            InfoRaised?.Invoke("Still working on the last drive or server. Try again in a moment.");
            return;
        }
        _busy = true;
        InfoRaised?.Invoke(working);
        try
        {
            await work();
        }
        finally
        {
            _busy = false;
        }
    }

    private static bool IsInside(string directory, string root)
    {
        string trimmed = Path.TrimEndingDirectorySeparator(root);
        return PathCompare.PathMatches(directory, trimmed)
            || directory.StartsWith(trimmed + Path.DirectorySeparatorChar, PathCompare.Comparison);
    }

    private static string Noun(MountKind kind) => kind switch
    {
        MountKind.Removable => "USB Drive",
        MountKind.Phone => "Phone",
        MountKind.Network => "Server",
        _ => "Disk",
    };

    private static string[] Keywords(MountKind kind) => kind switch
    {
        MountKind.Removable => ["usb", "stick", "removable", "drive", "mount", "eject"],
        MountKind.Phone => ["phone", "android", "mtp", "camera", "device", "mount"],
        MountKind.Network => ["network", "server", "share", "remote", "sftp", "smb", "ftp"],
        _ => ["disk", "partition", "volume", "drive", "mount"],
    };
}
