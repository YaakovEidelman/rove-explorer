using Rove.Core.Protocol;

namespace Rove.Core.Services;

public static class DriveFilter
{
    private static readonly string[] _alwaysShown =
    [
        "/media",
        "/mnt",
        "/run/media",
        "/Volumes",
    ];

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

    public static bool IsSystemMount(string mountPoint, string? fileSystem)
    {
        string path = Normalize(mountPoint);
        if (path.Length == 0)
            return true;

        if (path == "/")
            return false;

        if (fileSystem is { Length: > 0 } && _hiddenFilesystems.Contains(fileSystem))
            return true;

        if (IsDotted(path))
            return true;

        if (_alwaysShown.Any(prefix => IsUnder(path, prefix)))
            return false;

        return _hiddenMounts.Any(prefix => path == prefix || IsUnder(path, prefix));
    }

    private static bool IsDotted(string path) =>
        path.Split('/').Any(part => part.Length > 1 && part[0] == '.');

    private static string Normalize(string mountPoint)
    {
        string path = mountPoint.Trim();
        if (path.Length > 1)
            path = path.TrimEnd('/');
        return path.Length == 0 ? mountPoint.Trim() : path;
    }

    private static bool IsUnder(string path, string prefix) =>
        path.StartsWith(prefix + "/", StringComparison.Ordinal);

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
