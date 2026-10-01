using Rove.Core.Protocol;
using Rove.Core.Services;
using Rove.UI.Services;

namespace Rove.UI.ViewModels;

public sealed class MountsViewModel
{
    private const int SetupChecks = 200;

    public static readonly PaletteScope DrivePicker = new(CommandDef.DriveIdPrefix, "pick a drive…");

    private readonly ICommandTarget _registry;
    private readonly IMountService _service;
    private readonly IMountPrompter _prompter;
    private readonly Func<string, Task> _navigate;
    private readonly Func<string> _currentDirectory;
    private readonly DriveNumbers _numbers;
    private readonly DrivePlaces _places;
    private readonly IToolInstaller? _installer;
    private bool _busy;

    public MountsViewModel(
        ICommandTarget registry,
        IMountService service,
        IMountPrompter prompter,
        Func<string, Task> navigate,
        Func<string> currentDirectory,
        DriveNumbers? numbers = null,
        DrivePlaces? places = null,
        IToolInstaller? installer = null)
    {
        _registry = registry;
        _service = service;
        _prompter = prompter;
        _navigate = navigate;
        _currentDirectory = currentDirectory;
        _numbers = numbers ?? new DriveNumbers(null);
        _places = places ?? new DrivePlaces();
        _installer = installer;
    }

    public event Action<string>? ErrorRaised;

    public event Action<string>? InfoRaised;

    public event Action<PaletteScope>? PickRequested;

    public event Action<MountEntry[]>? ServersChanged;

    public MountEntry[] Servers { get; private set; } = [];

    internal TimeSpan SetupCheckDelay { get; set; } = TimeSpan.FromSeconds(3);

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

        Servers = [.. numbered.Where(entry => entry.Kind == MountKind.Network)];
        ServersChanged?.Invoke(Servers);

        HashSet<string> live = [];
        int order = CommandDef.MountOrder;
        MountEntry[] drives = [.. numbered.Where(entry => entry.Kind != MountKind.Network)];
        HashSet<string> shared = [.. drives.GroupBy(Label).Where(same => same.Count() > 1).Select(same => same.Key)];
        foreach (MountEntry entry in drives.OrderBy(e => KindRank(e.Kind)).ThenBy(e => e.Number ?? int.MaxValue)
                     .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ThenBy(e => e.Key, StringComparer.Ordinal))
        {
            string label = shared.Contains(Label(entry)) && entry.Device is { } device
                ? $"{Label(entry)} · {Path.GetFileName(device)}"
                : Label(entry);
            PaletteScope options = new($"{CommandDef.MountActionIdPrefix}{entry.Key}:", $"{label}: pick what to do…",
                DrivePicker);
            string driveId = CommandDef.MountIdPrefix + entry.Key;
            live.Add(driveId);
            _registry.Register(
                new CommandDef(driveId, $"{label}…", CommandKind.User, order++, CommandCategory.Navigation,
                    Keywords(entry.Kind), ShortTitle: label),
                () => PickRequested?.Invoke(options));
            foreach ((string verb, string title, string shortTitle, Func<Task> run) in Actions(entry, label, options))
            {
                string id = options.IdPrefix + verb;
                live.Add(id);
                _registry.Register(
                    new CommandDef(id, title, CommandKind.User, order++, CommandCategory.Navigation,
                        Keywords(entry.Kind), ShortTitle: shortTitle),
                    () => _ = run());
            }
        }

        order = CommandDef.SetupOrder;
        foreach (MountTool tool in _service.Missing)
        {
            string id = $"{CommandDef.MountSetupIdPrefix}{tool}";
            live.Add(id);
            _registry.Register(
                new CommandDef(id, SetupTitle(tool), CommandKind.User, order++, CommandCategory.Navigation,
                    ["setup", "install", "missing", .. Keywords(Needs(tool))], SetupShortTitle(tool)),
                () => _ = SetUpAsync(tool));
        }

        foreach (string id in _registry.CommandIdsStartingWith(CommandDef.MountIdPrefix)
                     .Concat(_registry.CommandIdsStartingWith(CommandDef.MountActionIdPrefix)).ToArray())
        {
            if (!live.Contains(id))
                _registry.Unregister(id);
        }
    }

    public async Task<bool> ConnectAsync(string address, IMountPrompter? prompter = null)
    {
        bool connected = false;
        await RunAsync($"Connecting to {address.Trim()}…", async () =>
        {
            CommandResult<string> result =
                await _service.ConnectAsync(address, prompter ?? _prompter, CancellationToken.None);
            connected = result.IsOk;
            await OpenAsync(result);
        });
        return connected;
    }

    public Task DisconnectAsync(MountEntry server) => UnmountAsync(server, eject: false);

    private async Task SetUpAsync(MountTool tool)
    {
        if (_installer?.CommandFor(tool) is not { } command)
        {
            ErrorRaised?.Invoke(SetupHelp(tool));
            return;
        }

        string shown = string.Join(' ', command);
        if (await _prompter.ChooseAsync(
                $"{SetupReason(tool)} Install it now? A terminal opens and runs:\n{shown}",
                ["Install in a terminal"]) is null)
            return;

        if (_installer.RunInTerminal(command) is { } error)
        {
            ErrorRaised?.Invoke($"{error} You can run this yourself: {shown}");
            return;
        }

        if (tool != MountTool.UDisks)
        {
            InfoRaised?.Invoke("When the install finishes, open Rove again to use phones and servers.");
            return;
        }

        InfoRaised?.Invoke("Installing UDisks in the terminal…");
        for (int check = 0; check < SetupChecks; check++)
        {
            await Task.Delay(SetupCheckDelay);
            await RefreshAsync();
            if (!_service.Missing.Contains(MountTool.UDisks))
            {
                InfoRaised?.Invoke("UDisks is installed. Press g to see your USB drives.");
                return;
            }
        }
    }

    private IEnumerable<(string Verb, string Title, string ShortTitle, Func<Task> Run)> Actions(
        MountEntry entry, string label, PaletteScope options)
    {
        string eject = entry.Kind == MountKind.Image ? "Detach" : "Eject";
        if (!entry.IsMounted)
        {
            if (entry.CanMount)
                yield return ("open", $"Open {label}", "Open", () => MountAsync(entry));
            if (entry.Kind == MountKind.Image)
                yield return ("eject", $"Detach {label}", "Detach", () => UnmountAsync(entry, eject: true));
            if (entry.Kind == MountKind.Removable)
                yield return ("format", $"Format {label}…", "Format…", () => PickFormatAsync(entry, options));
            yield break;
        }

        if (entry.LocalPath is { } path)
            yield return ("goto", $"Go to {label}", "Go to", () => GoToAsync(entry, path));

        if (entry.CanUnmount)
            yield return ("unmount", $"Unmount {label}", "Unmount", () => UnmountAsync(entry, eject: false));
        if (entry.CanEject)
            yield return ("eject", $"{eject} {label}", eject, () => UnmountAsync(entry, eject: true));
        if (entry.Kind == MountKind.Removable)
            yield return ("format", $"Format {label}…", "Format…", () => PickFormatAsync(entry, options));
    }

    private Task MountAsync(MountEntry entry) => RunAsync($"Opening {entry.Name}…", async () =>
    {
        CommandResult<string> result = await _service.MountAsync(entry, _prompter, CancellationToken.None);
        await OpenAsync(result, entry);
    });

    private Task UnmountAsync(MountEntry entry, bool eject) =>
        RunAsync(eject ? $"{(entry.Kind == MountKind.Image ? "Detaching" : "Ejecting")} {Label(entry)}…"
            : $"Unmounting {Label(entry)}…", async () =>
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
                ? entry.Kind == MountKind.Image ? $"Detached {label}." : $"Ejected {label}. It's safe to remove."
                : entry.Kind == MountKind.Network ? $"Disconnected {label}." : $"Unmounted {label}.");
            await RefreshAsync();
        });

    private async Task PickFormatAsync(MountEntry entry, PaletteScope options)
    {
        DriveFormat[] formats = await _service.FormatsAsync(CancellationToken.None);
        foreach (string id in _registry.CommandIdsStartingWith(CommandDef.FormatAsIdPrefix))
            _registry.Unregister(id);
        if (formats.Length == 0)
        {
            ErrorRaised?.Invoke("Formatting needs UDisks and mkfs tools like exfatprogs or dosfstools.");
            return;
        }

        for (int i = 0; i < formats.Length; i++)
        {
            DriveFormat format = formats[i];
            _registry.Register(
                new CommandDef(CommandDef.FormatAsIdPrefix + format.Type, $"{format.Name} ({format.Note})",
                    CommandKind.User, i, CommandCategory.Navigation),
                () => _ = FormatAsync(entry, format));
        }
        PickRequested?.Invoke(new(CommandDef.FormatAsIdPrefix, $"format {Label(entry)} as…", options));
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

    private async Task OpenAsync(CommandResult<string> result, MountEntry? entry = null)
    {
        if (!result.IsOk || result.Data is not { } path)
        {
            ErrorRaised?.Invoke(result.Message ?? "Couldn't open that location.");
            return;
        }
        InfoRaised?.Invoke(string.Empty);
        await RefreshAsync();
        if (entry is null)
            await _navigate(path);
        else
            await GoToAsync(entry, path);
    }

    private async Task GoToAsync(MountEntry entry, string path)
    {
        await _navigate(path);
        if (entry.Kind == MountKind.Phone && await Task.Run(() => IsEmpty(path)))
            InfoRaised?.Invoke(
                $"{entry.Name} isn't sharing its files yet. Unlock the phone and tap Allow, "
                + "or pick File transfer in its USB notification, then open it again.");
    }

    private static bool IsEmpty(string path)
    {
        try
        {
            return !Directory.EnumerateFileSystemEntries(path).Any();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
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
        MountKind.Image => "Image",
        _ => "Disk",
    };

    private static MountKind Needs(MountTool tool) => tool == MountTool.UDisks ? MountKind.Removable : MountKind.Network;

    private static string SetupTitle(MountTool tool) => tool == MountTool.UDisks
        ? "Set up USB drives (UDisks isn't installed)"
        : "Set up phones and servers (gio isn't installed)";

    private static int KindRank(MountKind kind) => kind switch
    {
        MountKind.Removable => 0,
        MountKind.Disk => 1,
        MountKind.Image => 2,
        MountKind.Phone => 3,
        _ => 3,
    };

    private static string SetupShortTitle(MountTool tool) => tool == MountTool.UDisks
        ? "USB drives: install UDisks"
        : "Phones and servers: install gio";

    private static string SetupReason(MountTool tool) => tool == MountTool.UDisks
        ? "Rove can't see USB drives because UDisks, the system disk service, isn't installed."
        : "Phones and servers need gio and gvfs, which aren't installed.";

    private static string SetupHelp(MountTool tool) => tool == MountTool.UDisks
        ? $"{SetupReason(tool)} Install the udisks2 and polkit packages, then open Rove again."
        : $"{SetupReason(tool)} Install the glib2 and gvfs packages, then open Rove again.";

    private static string[] Keywords(MountKind kind) => kind switch
    {
        MountKind.Removable => ["usb", "stick", "removable", "drive", "mount", "eject"],
        MountKind.Phone => ["phone", "android", "mtp", "camera", "device", "mount"],
        MountKind.Network => ["network", "server", "share", "remote", "sftp", "smb", "ftp"],
        MountKind.Image => ["image", "iso", "loop", "disk image", "mount", "detach"],
        _ => ["disk", "partition", "volume", "drive", "mount"],
    };
}
