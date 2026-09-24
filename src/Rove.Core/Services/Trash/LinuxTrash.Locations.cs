using System.Runtime.InteropServices;

namespace Rove.Core.Services;

public static partial class LinuxTrash
{
    private static string? TrashHolding(string fullPath)
    {
        if (Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(fullPath)) is not { Length: > 0 } filesDir)
            return null;
        if (!string.Equals(Path.GetFileName(filesDir), FilesDir, StringComparison.Ordinal))
            return null;
        if (Path.GetDirectoryName(filesDir) is not { Length: > 0 } trash)
            return null;
        return Directory.Exists(Path.Combine(trash, InfoDir)) ? trash : null;
    }

    private static string HomeTrash() => Path.Combine(XdgPaths.DataHome, "Trash");

    private static string? ResolveTrashDirectory(string full, out string? topDir, out string? problem)
    {
        topDir = null;
        problem = null;

        string? volume = VolumeOf(full);
        if (volume is null || IsHomeVolume(volume))
            return HomeTrash();

        foreach (string candidate in VolumeTrashCandidates(volume))
        {
            if (Directory.Exists(candidate) || TryCreate(candidate))
            {
                topDir = volume;
                return candidate;
            }
        }

        problem = $"There is nowhere to put a trash on {volume}, so this can only be deleted permanently.";
        return null;
    }

    private static IEnumerable<string> VolumeTrashCandidates(string volume)
    {
        string shared = Path.Combine(volume, ".Trash");
        if (IsUsableSharedTrash(shared))
            yield return Path.Combine(shared, UserId().ToString());

        yield return Path.Combine(volume, $".Trash-{UserId()}");
    }

    private static bool IsUsableSharedTrash(string shared)
    {
        try
        {
            if (!Directory.Exists(shared))
                return false;
            DirectoryInfo info = new(shared);
            if (info.LinkTarget is not null)
                return false;
            return !OperatingSystem.IsLinux()
                || File.GetUnixFileMode(shared).HasFlag(UnixFileMode.StickyBit);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            return false;
        }
    }

    private static bool IsHomeVolume(string volume) =>
        VolumeOf(XdgPaths.DataHome) is { } home && PathCompare.PathMatches(home, volume);

    private static string? VolumeOf(string fullPath)
    {
        string? best = null;
        try
        {
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                string root;
                try
                {
                    root = Path.TrimEndingDirectorySeparator(drive.RootDirectory.FullName);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    continue;
                }
                if (root.Length == 0)
                    root = Path.DirectorySeparatorChar.ToString();
                if (!IsUnderOrEqual(fullPath, root))
                    continue;
                if (best is null || root.Length > best.Length)
                    best = root;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
        return best;
    }

    private static string? TopDirectoryOfTrash(string trash)
    {
        DirectoryInfo? dir = new DirectoryInfo(trash).Parent;
        if (dir is null)
            return null;
        if (dir.Name == ".Trash")
            dir = dir.Parent;
        return dir?.FullName;
    }

    private static uint UserId()
    {
        if (!OperatingSystem.IsLinux())
            return 0;
        try
        {
            return getuid();
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return 0;
        }
    }

    [DllImport("libc", SetLastError = true)]
    private static extern uint getuid();
}
