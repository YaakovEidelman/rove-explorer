using Rove.Core.Protocol;

namespace Rove.Core.Services;

/// <summary>
/// Linux hangs nearly everything off the same tree, so the raw mount list is
/// mostly not places to browse: kernel bookkeeping (/proc, /sys), memory-only
/// scratch space (/run, /dev/shm), and one read-only loopback per installed
/// snap or container layer. This keeps the real disks and anything the system
/// mounted for the user — removable media, network shares, extra partitions —
/// and drops the rest.
/// </summary>
public static class DriveFilter
{
    /// <summary>
    /// Where the desktop puts disks it mounts for you. Always shown, whatever
    /// they sit under — /run/media is the usual spot on Fedora and Arch, and
    /// the /run rule below would otherwise swallow it.
    /// </summary>
    private static readonly string[] _alwaysShown =
    [
        "/media",
        "/mnt",
        "/run/media",
        "/Volumes",
    ];

    /// <summary>
    /// Mount points that are the system talking to itself. A distribution on
    /// btrfs or ostree mounts half of these as separate volumes — /var, /usr,
    /// /etc and friends are the OS's own furniture, reachable by walking down
    /// from / when anyone actually wants them, and only clutter a list of
    /// places to go.
    /// </summary>
    private static readonly string[] _hiddenMounts =
    [
        "/proc",
        "/sys",
        "/dev",
        "/run",
        "/snap",
        "/var",
        "/usr",
        "/etc",
        "/boot",
        "/efi",
        "/sysroot",
        "/nix/store",
        "/opt/containerd",
        "/tmp",
        "/System",
        "/init",
    ];

    /// <summary>
    /// Filesystems with no storage behind them: kernel interfaces, RAM-backed
    /// scratch, and the read-only images snaps and containers are built from.
    /// </summary>
    private static readonly HashSet<string> _hiddenFilesystems = new(StringComparer.OrdinalIgnoreCase)
    {
        "autofs",
        "binfmt_misc",
        "bpf",
        "cgroup",
        "cgroup2",
        "configfs",
        "debugfs",
        "devpts",
        "devtmpfs",
        "efivarfs",
        "erofs",
        "fuse.gvfsd-fuse",
        "fuse.portal",
        "fuse.snapfuse",
        "fusectl",
        "hugetlbfs",
        "mqueue",
        "nsfs",
        "overlay",
        "overlayfs",
        "pstore",
        "ramfs",
        "resctrl",
        "rpc_pipefs",
        "securityfs",
        "selinuxfs",
        "squashfs",
        "sunrpc",
        "sysfs",
        "tmpfs",
        "tracefs",
        "proc",
    };

    /// <summary>
    /// True when a mount is the system's own plumbing rather than somewhere
    /// the user would ever want to open. Windows and macOS drive lists are
    /// already short, so only the Linux listing asks.
    /// </summary>
    public static bool IsSystemMount(string mountPoint, string? fileSystem)
    {
        string path = Normalize(mountPoint);
        if (path.Length == 0)
            return true;

        // The root filesystem is the one mount that is always worth showing.
        if (path == "/")
            return false;

        // Nothing with storage behind it is ever worth listing, wherever it
        // was mounted — the desktop's own media folders included, which is
        // where WSL parks its scratch mounts.
        if (fileSystem is { Length: > 0 } && _hiddenFilesystems.Contains(fileSystem))
            return true;

        // Something mounted at a dotted name (/.snapshots, /tmp/.mount_abc)
        // was put there to be out of the way.
        if (IsDotted(path))
            return true;

        if (_alwaysShown.Any(prefix => IsUnder(path, prefix)))
            return false;

        return _hiddenMounts.Any(prefix => path == prefix || IsUnder(path, prefix));
    }

    /// <summary>True when any part of the path is a dot-name.</summary>
    private static bool IsDotted(string path) =>
        path.Split('/').Any(part => part.Length > 1 && part[0] == '.');

    /// <summary>Trailing slashes off, backslashes never appear here.</summary>
    private static string Normalize(string mountPoint)
    {
        string path = mountPoint.Trim();
        if (path.Length > 1)
            path = path.TrimEnd('/');
        return path.Length == 0 ? mountPoint.Trim() : path;
    }

    /// <summary>Inside <paramref name="prefix"/>, not merely starting with its letters.</summary>
    private static bool IsUnder(string path, string prefix) =>
        path.StartsWith(prefix + "/", StringComparison.Ordinal);

    /// <summary>
    /// One entry per place, in the order they came. The mount table can name
    /// the same directory twice — a live system's root sits under both its
    /// image and its overlay — and two rows saying "Go to Drive /" are one
    /// row's worth of use.
    /// </summary>
    public static DriveEntry[] Deduplicate(IEnumerable<DriveEntry> drives)
    {
        HashSet<string> seen = new(PathCompare.Comparer);
        List<DriveEntry> kept = [];
        foreach (DriveEntry drive in drives)
        {
            if (seen.Add(Path.TrimEndingDirectorySeparator(drive.RootPath)))
                kept.Add(drive);
        }
        return [.. kept];
    }
}
