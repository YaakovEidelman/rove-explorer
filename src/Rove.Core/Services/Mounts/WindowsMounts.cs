using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Versioning;
using Rove.Core.Protocol;

namespace Rove.Core.Services;

[SupportedOSPlatform("windows")]
public sealed partial class WindowsMounts : IMountService
{
    private const int UserCancelled = 1223;
    private static readonly TimeSpan _pollEvery = TimeSpan.FromSeconds(2);

    private readonly MountChangeSignal _change = new();
    private Timer? _poll;
    private string _seen = "";

    public event Action? Changed
    {
        add => _change.Fired += value;
        remove => _change.Fired -= value;
    }

    public IReadOnlyList<MountTool> Missing => [];

    public void StartWatching()
    {
        if (_poll is not null)
            return;
        _seen = Signature();
        _poll = new Timer(_ =>
        {
            string now = Signature();
            if (now == _seen)
                return;
            _seen = now;
            _change.Raise();
        }, null, _pollEvery, _pollEvery);
    }

    public Task<MountEntry[]> ListAsync(CancellationToken ct) =>
        Task.Run(() => WindowsDriveList.Build(UsbVolumes()), ct);

    public Task<CommandResult<string>> MountAsync(MountEntry entry, IMountPrompter prompter, CancellationToken ct) =>
        Task.FromResult(entry.LocalPath is { } path
            ? CommandResult<string>.Ok(path)
            : CommandResult<string>.Fail("not_found", $"{entry.Name} isn't plugged in anymore."));

    public Task<CommandResult<string>> ConnectAsync(string address, IMountPrompter prompter, CancellationToken ct) =>
        Task.FromResult(CommandResult<string>.Fail(
            "no_remote", "Rove can't connect to servers on Windows yet. Map a network drive and it shows up as a drive."));

    public Task<CommandResult<bool>> UnmountAsync(MountEntry entry, bool eject, CancellationToken ct) =>
        Task.Run(() => entry.LocalPath is { } root && Eject(root) is { } problem
            ? CommandResult<bool>.Fail("unmount_failed", problem)
            : CommandResult<bool>.Ok(true), ct);

    public Task<DriveFormat[]> FormatsAsync(CancellationToken ct) =>
        Task.FromResult<DriveFormat[]>([.. DriveFormat.All.Where(format => WindowsFileSystem(format.Type) is not null)]);

    public async Task<CommandResult<string>> FormatAsync(
        MountEntry entry, DriveFormat format, string name, CancellationToken ct)
    {
        if (entry.LocalPath is not { Length: > 0 } root || !char.IsAsciiLetter(root[0])
            || WindowsFileSystem(format.Type) is not { } fileSystem)
            return CommandResult<string>.Fail("format_failed", $"Couldn't format {entry.Name}.");

        string script = $"try {{ Format-Volume -DriveLetter {root[0]} -FileSystem {fileSystem} "
            + $"-NewFileSystemLabel '{name.Replace("'", "''")}' -Confirm:$false -ErrorAction Stop | Out-Null; exit 0 }} "
            + "catch { exit 1 }";
        ProcessStartInfo start = new("powershell.exe")
        {
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden,
        };
        foreach (string argument in new[] { "-NoProfile", "-NonInteractive", "-Command", script })
            start.ArgumentList.Add(argument);

        try
        {
            using Process? process = Process.Start(start);
            if (process is null)
                return CommandResult<string>.Fail("format_failed", $"Couldn't format {entry.Name}.");
            await process.WaitForExitAsync(ct).ConfigureAwait(false);
            if (process.ExitCode != 0)
                return CommandResult<string>.Fail("format_failed",
                    $"Windows couldn't format {entry.Name}. FAT32 only works on drives up to 32 GB.");
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == UserCancelled)
        {
            return CommandResult<string>.Fail("cancelled", $"Left {entry.Name} as it was.");
        }
        catch (Win32Exception ex)
        {
            return CommandResult<string>.Fail("format_failed", ex.Message);
        }

        uint serial = Serial(root);
        return CommandResult<string>.Ok(serial == 0 ? "" : WindowsDriveList.SerialText(serial));
    }

    public void Dispose()
    {
        _poll?.Dispose();
        _change.Dispose();
    }

    private static string? WindowsFileSystem(string type) => type switch
    {
        "exfat" => "exFAT",
        "vfat" => "FAT32",
        "ntfs" => "NTFS",
        _ => null,
    };

    private static IEnumerable<WindowsVolume> UsbVolumes() =>
        Drives().Select(UsbVolume).OfType<WindowsVolume>();

    private static WindowsVolume? UsbVolume(DriveInfo drive)
    {
        if (drive.DriveType is not (DriveType.Removable or DriveType.Fixed))
            return null;
        try
        {
            if (!drive.IsReady)
                return null;
            string root = drive.RootDirectory.FullName;
            (bool usb, string? model) = Describe(root);
            return drive.DriveType == DriveType.Fixed && !usb
                ? null
                : new WindowsVolume(root, drive.VolumeLabel, model, Serial(root), drive.TotalSize);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static DriveInfo[] Drives()
    {
        try
        {
            return DriveInfo.GetDrives();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static string Signature() =>
        string.Join('|', Drives().Select(drive => $"{drive.Name}{Ready(drive)}"));

    private static bool Ready(DriveInfo drive)
    {
        try
        {
            return drive.IsReady;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
