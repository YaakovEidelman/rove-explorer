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
    private readonly DriveNumbers _numbers;
    private readonly DrivePlaces _places;
    private bool _busy;

    public MountsViewModel(
        ICommandTarget registry,
        IMountService service,
        IMountPrompter prompter,
        Func<string, Task> navigate,
        Func<string> currentDirectory,
        DriveNumbers? numbers = null,
        DrivePlaces? places = null)
    {
        _registry = registry;
        _service = service;
        _prompter = prompter;
        _navigate = navigate;
        _currentDirectory = currentDirectory;
        _numbers = numbers ?? new DriveNumbers(null);
        _places = places ?? new DrivePlaces();
        _registry.Register(CommandDef.ConnectToServer, () => _ = ConnectToServerAsync());
    }

    public event Action<string>? ErrorRaised;

    public event Action<string>? InfoRaised;

    public event Action<string, string>? PickRequested;

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
        MountEntry[] numbered = _numbers.Assign([.. entries]);
        _places.Set(numbered
            .Where(entry => entry.LocalPath is not null)
            .Select(entry => (entry.LocalPath!, Label(entry))));

        HashSet<string> live = [];
        foreach (MountEntry entry in numbered)
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

        foreach (MountTool tool in _service.Missing)
        {
            string id = $"{CommandDef.MountIdPrefix}setup:{tool}";
            live.Add(id);
            _registry.Register(
                new CommandDef(id, SetupTitle(tool), CommandKind.User, Category: CommandCategory.Navigation,
                    Keywords: ["setup", "install", "missing", .. Keywords(Needs(tool))]),
                () => ErrorRaised?.Invoke(SetupHelp(tool)));
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
        string label = Label(entry);
        if (!entry.IsMounted)
        {
            if (entry.CanMount)
                yield return ("open", $"Open {label}", () => MountAsync(entry));
            if (entry.Kind == MountKind.Removable)
                yield return ("format", $"Format {label}…", () => PickFormatAsync(entry));
            yield break;
        }

        if (entry.LocalPath is { } path)
            yield return ("goto", $"Go to {label}", () => _navigate(path));

        if (entry.CanUnmount)
            yield return entry.Kind == MountKind.Network
                ? ("unmount", $"Disconnect {label}", () => UnmountAsync(entry, eject: false))
                : ("unmount", $"Unmount {label}", () => UnmountAsync(entry, eject: false));
        if (entry.CanEject)
            yield return ("eject", $"Eject {label}", () => UnmountAsync(entry, eject: true));
        if (entry.Kind == MountKind.Removable)
            yield return ("format", $"Format {label}…", () => PickFormatAsync(entry));
    }

    private Task MountAsync(MountEntry entry) => RunAsync($"Opening {entry.Name}…", async () =>
    {
        CommandResult<string> result = await _service.MountAsync(entry, _prompter, CancellationToken.None);
        await OpenAsync(result);
    });

    private Task UnmountAsync(MountEntry entry, bool eject) =>
        RunAsync(eject ? $"Ejecting {Label(entry)}…" : $"Unmounting {Label(entry)}…", async () =>
        {
            if (entry.LocalPath is { } root && IsInside(_currentDirectory(), root))
                await _navigate(PathCompare.DefaultStartDirectory());

            CommandResult<bool> result = await _service.UnmountAsync(entry, eject, CancellationToken.None);
            if (!result.IsOk)
            {
                ErrorRaised?.Invoke(result.Message ?? $"Couldn't unmount {entry.Name}.");
                return;
            }
            string label = Label(entry);
            InfoRaised?.Invoke(eject
                ? $"Ejected {label}. It's safe to remove."
                : entry.Kind == MountKind.Network ? $"Disconnected {label}." : $"Unmounted {label}.");
            await RefreshAsync();
        });

    private async Task PickFormatAsync(MountEntry entry)
    {
        DriveFormat[] formats = await _service.FormatsAsync(CancellationToken.None);
        foreach (string id in _registry.CommandIdsStartingWith(CommandDef.FormatAsIdPrefix))
            _registry.Unregister(id);
        if (formats.Length == 0)
        {
            ErrorRaised?.Invoke("Formatting needs UDisks and mkfs tools like exfatprogs or dosfstools.");
            return;
        }

        foreach (DriveFormat format in formats)
        {
            _registry.Register(
                new CommandDef(CommandDef.FormatAsIdPrefix + format.Type, $"{format.Name} ({format.Note})",
                    CommandKind.User, Category: CommandCategory.Navigation),
                () => _ = FormatAsync(entry, format));
        }
        PickRequested?.Invoke(CommandDef.FormatAsIdPrefix, $"format {Label(entry)} as…");
    }

    private async Task FormatAsync(MountEntry entry, DriveFormat format)
    {
        string label = Label(entry);
        string current = entry.VolumeLabel ?? "";
        string suggested = current.Length > format.NameLimit ? current[..format.NameLimit] : current;
        if (await _prompter.AskTextAsync(
                $"Name for the drive, up to {format.NameLimit} characters.", "Name", secret: false, suggested)
            is not { } typed)
            return;
        string name = typed.Trim();
        if (name.Length > format.NameLimit)
            name = name[..format.NameLimit];

        if (await _prompter.ChooseAsync(
                $"Erase everything on {label} and format it as {format.Name}? This can't be undone.",
                ["Erase and format"]) is null)
        {
            InfoRaised?.Invoke($"Left {label} as it was.");
            return;
        }

        await RunAsync($"Formatting {label} as {format.Name}…", async () =>
        {
            if (entry.LocalPath is { } root && IsInside(_currentDirectory(), root))
                await _navigate(PathCompare.DefaultStartDirectory());

            CommandResult<string> result = await _service.FormatAsync(entry, format, name, CancellationToken.None);
            if (!result.IsOk)
            {
                ErrorRaised?.Invoke(result.Message ?? $"Couldn't format {label}.");
                return;
            }
            if (entry.VolumeId is { } old && result.Data is { Length: > 0 } fresh)
                _numbers.Move(old, fresh);
            InfoRaised?.Invoke($"Formatted {label} as {format.Name}.");
            await RefreshAsync();
        });
    }

    private async Task OpenAsync(CommandResult<string> result)
    {
        if (!result.IsOk || result.Data is not { } path)
        {
            ErrorRaised?.Invoke(result.Message ?? "Couldn't open that location.");
            return;
        }
        InfoRaised?.Invoke(string.Empty);
        await RefreshAsync();
        await _navigate(path);
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
            || directory.StartsWith(trimmed + Path.DirectorySeparatorChar, PathCompare.Comparison)
            || directory.StartsWith(trimmed + Path.AltDirectorySeparatorChar, PathCompare.Comparison);
    }

    private static string Label(MountEntry entry) => entry.Number is { } number
        ? $"{(entry.Kind == MountKind.Removable ? "USB" : "Disk")} {number}: {entry.Name}"
        : $"{Noun(entry.Kind)} {entry.Name}";

    private static string Noun(MountKind kind) => kind switch
    {
        MountKind.Removable => "USB Drive",
        MountKind.Phone => "Phone",
        MountKind.Network => "Server",
        _ => "Disk",
    };

    private static MountKind Needs(MountTool tool) => tool == MountTool.UDisks ? MountKind.Removable : MountKind.Network;

    private static string SetupTitle(MountTool tool) => tool == MountTool.UDisks
        ? "Set up USB drives (UDisks isn't installed)"
        : "Set up phones and servers (gio isn't installed)";

    private static string SetupHelp(MountTool tool) => tool == MountTool.UDisks
        ? "Rove can't see USB drives because UDisks, the system disk service, isn't installed. "
            + "Install the udisks2 and polkit packages, then open Rove again."
        : "Phones and servers need gio and gvfs. Install the glib2 and gvfs packages, then open Rove again.";

    private static string[] Keywords(MountKind kind) => kind switch
    {
        MountKind.Removable => ["usb", "stick", "removable", "drive", "mount", "eject"],
        MountKind.Phone => ["phone", "android", "mtp", "camera", "device", "mount"],
        MountKind.Network => ["network", "server", "share", "remote", "sftp", "smb", "ftp"],
        _ => ["disk", "partition", "volume", "drive", "mount"],
    };
}
