using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Rove.Core.Protocol;

namespace Rove.Core.Services;

/// <summary>
/// The desktop trash on Linux, as every file manager there implements it
/// (the freedesktop.org Trash specification). There is no system call for
/// it: a trash is a plain directory holding <c>files/</c> — the items
/// themselves, moved in under a name that is free — and <c>info/</c>, one
/// small text file per item saying where it came from and when it went.
/// That pairing is what lets anything, Rove included, put an item back.
///
/// <para>
/// An item is only ever <em>moved</em>, never copied, so the trash used has
/// to sit on the same disk as the item. Things in the home directory go to
/// the trash under <c>~/.local/share</c>; something on a USB stick or a
/// second drive goes to a trash at the top of that disk instead. When
/// neither is possible this says so rather than quietly copying gigabytes
/// across a cable.
/// </para>
/// </summary>
[SupportedOSPlatform("linux")]
public static class LinuxTrash
{
    private const string FilesDir = "files";
    private const string InfoDir = "info";
    private const string InfoSuffix = ".trashinfo";

    /// <summary>Enough tries at a free name that a real collision run is covered.</summary>
    private const int MaxNameAttempts = 1024;

    // ── trashing ─────────────────────────────────────────────────────────

    public static CommandResult<string?> MoveToTrash(string[] paths)
    {
        foreach (string path in paths)
        {
            CommandResult<string?> one = TrashOne(path);
            if (!one.IsOk)
                return one;
        }
        return CommandResult<string?>.Ok(null);
    }

    private static CommandResult<string?> TrashOne(string path)
    {
        string full = Path.GetFullPath(path);
        if (!File.Exists(full) && !Directory.Exists(full))
            return CommandResult<string?>.Fail("not_found", $"No such item: {path}");

        string? trash = ResolveTrashDirectory(full, out string? topDir, out string? problem);
        if (trash is null)
            return CommandResult<string?>.Fail("trash_unavailable", problem);

        try
        {
            Directory.CreateDirectory(Path.Combine(trash, FilesDir));
            Directory.CreateDirectory(Path.Combine(trash, InfoDir));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return CommandResult<string?>.Fail("trash_failed", $"Could not open the trash at {trash} ({ex.Message}).");
        }

        // The path recorded is relative to the disk's own trash, absolute in
        // the home one — that way a removable disk's trash still makes sense
        // wherever it is next plugged in.
        string recorded = topDir is null ? full : Path.GetRelativePath(topDir, full);

        return MoveIntoTrash(full, trash, recorded);
    }

    private static CommandResult<string?> MoveIntoTrash(string full, string trash, string recorded)
    {
        string name = Path.GetFileName(Path.TrimEndingDirectorySeparator(full));
        string stem = Path.GetFileNameWithoutExtension(name);
        string extension = Path.GetExtension(name);

        for (int attempt = 0; attempt < MaxNameAttempts; attempt++)
        {
            string candidate = attempt == 0 ? name : $"{stem}.{attempt}{extension}";
            string infoPath = Path.Combine(trash, InfoDir, candidate + InfoSuffix);
            string filePath = Path.Combine(trash, FilesDir, candidate);

            // Something already parked under that name, with no info file to
            // go with it — leave it alone and take the next name.
            if (File.Exists(filePath) || Directory.Exists(filePath))
                continue;

            // The info file is written first, and only if the name is still
            // free: creating it is what claims the name, so two file managers
            // trashing at once cannot land on the same one.
            try
            {
                using (FileStream claim = new(infoPath, FileMode.CreateNew, FileAccess.Write))
                using (StreamWriter writer = new(claim))
                {
                    writer.Write(InfoContents(recorded, DateTime.Now));
                }
            }
            catch (IOException) when (File.Exists(infoPath))
            {
                continue; // taken in the meantime
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return CommandResult<string?>.Fail("trash_failed", $"Could not write to the trash ({ex.Message}).");
            }

            try
            {
                Move(full, filePath);
                return CommandResult<string?>.Ok(null);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                TryDelete(infoPath); // nothing moved, so leave no record saying it did
                return CommandResult<string?>.Fail("trash_failed", $"Could not move {name} to the trash ({ex.Message}).");
            }
        }

        return CommandResult<string?>.Fail("trash_failed", $"The trash already holds too many things named {name}.");
    }

    /// <summary>The body of a .trashinfo file — plain text, in the order the spec gives.</summary>
    internal static string InfoContents(string recordedPath, DateTime deletedAt) =>
        $"[Trash Info]\nPath={EncodePath(recordedPath)}\nDeletionDate={deletedAt:yyyy-MM-ddTHH:mm:ss}\n";

    // ── putting things back ──────────────────────────────────────────────

    /// <summary>
    /// Moves items back to where they were trashed from. Returns the paths
    /// that actually came back; anything no longer in a trash is simply not
    /// among them.
    /// </summary>
    public static CommandResult<string[]> RestoreFromTrash(string[] paths)
    {
        List<string> restored = [];

        foreach (string path in paths)
        {
            string full = Path.GetFullPath(path);
            foreach (string trash in TrashesToSearch(full))
            {
                if (FindInTrash(trash, full) is not { } found)
                    continue;
                if (RestoreOne(found.FilePath, found.InfoPath, full))
                    restored.Add(path);
                break;
            }
        }

        return CommandResult<string[]>.Ok([.. restored]);
    }

    private static bool RestoreOne(string filePath, string infoPath, string destination)
    {
        if (File.Exists(destination) || Directory.Exists(destination))
            return false; // something took the name back; do not clobber it

        try
        {
            if (Path.GetDirectoryName(destination) is { Length: > 0 } parent)
                Directory.CreateDirectory(parent);
            Move(filePath, destination);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }

        TryDelete(infoPath);
        return true;
    }

    /// <summary>The item in <paramref name="trash"/> that came from <paramref name="original"/>.</summary>
    private static (string FilePath, string InfoPath)? FindInTrash(string trash, string original)
    {
        string infoRoot = Path.Combine(trash, InfoDir);
        if (!Directory.Exists(infoRoot))
            return null;

        string? topDir = TopDirectoryOfTrash(trash);

        IEnumerable<string> infoFiles;
        try
        {
            infoFiles = Directory.EnumerateFiles(infoRoot, "*" + InfoSuffix);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        foreach (string infoPath in infoFiles)
        {
            if (ReadRecordedPath(infoPath) is not { } recorded)
                continue;

            string was = Path.IsPathRooted(recorded) || topDir is null
                ? recorded
                : Path.GetFullPath(Path.Combine(topDir, recorded));
            if (!PathCompare.PathMatches(was, original))
                continue;

            string name = Path.GetFileName(infoPath)[..^InfoSuffix.Length];
            string filePath = Path.Combine(trash, FilesDir, name);
            if (File.Exists(filePath) || Directory.Exists(filePath))
                return (filePath, infoPath);
        }
        return null;
    }

    /// <summary>The <c>Path=</c> line of a .trashinfo file, decoded.</summary>
    internal static string? ReadRecordedPath(string infoPath)
    {
        try
        {
            foreach (string line in File.ReadLines(infoPath))
            {
                if (line.StartsWith("Path=", StringComparison.Ordinal))
                    return DecodePath(line["Path=".Length..].Trim());
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
        return null;
    }

    /// <summary>Home trash first, then the trash on the item's own disk.</summary>
    private static IEnumerable<string> TrashesToSearch(string full)
    {
        yield return HomeTrash();

        if (VolumeOf(full) is not { } volume || IsHomeVolume(volume))
            yield break;
        foreach (string candidate in VolumeTrashCandidates(volume))
            yield return candidate;
    }

    // ── browsing the trash ───────────────────────────────────────────────
    // A trash is a folder, so Rove can simply walk into it. What sits in
    // files/ is the items themselves; the record saying where each came from
    // is the matching file in info/, which is what "put this back" reads.

    /// <summary>The folder holding the trashed items themselves.</summary>
    public static string BrowsePath() => Path.Combine(HomeTrash(), FilesDir);

    /// <summary>
    /// Puts items that are sitting in a trash right now back where they came
    /// from, named by where they are in the trash rather than by where they
    /// used to live.
    /// </summary>
    public static OpResult[] RestoreTrashedPaths(string[] paths) =>
        [.. paths.Select(RestoreTrashedPath)];

    private static OpResult RestoreTrashedPath(string path)
    {
        string full = Path.GetFullPath(path);
        string name = Path.GetFileName(Path.TrimEndingDirectorySeparator(full));

        if (!File.Exists(full) && !Directory.Exists(full))
            return OpResult.Failure(path, "not_found", $"No such item: {path}");

        if (TrashHolding(full) is not { } trash)
        {
            return OpResult.Failure(path, "not_in_trash",
                $"{name} is not a whole item in the trash, so there is nothing saying where it belongs.");
        }

        string infoPath = Path.Combine(trash, InfoDir, name + InfoSuffix);
        if (ReadRecordedPath(infoPath) is not { } recorded)
        {
            return OpResult.Failure(path, "no_record",
                $"The trash has no record of where {name} came from, so it can only be moved by hand.");
        }

        string? topDir = TopDirectoryOfTrash(trash);
        string destination = Path.IsPathRooted(recorded) || topDir is null
            ? recorded
            : Path.GetFullPath(Path.Combine(topDir, recorded));

        if (File.Exists(destination) || Directory.Exists(destination))
        {
            return OpResult.Failure(path, "already_exists",
                $"Something is at {destination} again, so {name} was left in the trash.");
        }

        if (!RestoreOne(full, infoPath, destination))
            return OpResult.Failure(path, "restore_failed", $"{name} could not be moved back to {destination}.");

        return OpResult.Success(path, FolderItem.FromPath(destination));
    }

    /// <summary>
    /// Drops the record for something that was in the trash and has now been
    /// deleted outright — an info file with nothing to describe is litter.
    /// </summary>
    public static void ForgetRecord(string path)
    {
        string full = Path.GetFullPath(path);
        if (TrashHolding(full) is not { } trash)
            return;
        string name = Path.GetFileName(Path.TrimEndingDirectorySeparator(full));
        TryDelete(Path.Combine(trash, InfoDir, name + InfoSuffix));
    }

    /// <summary>
    /// The trash a path is an item of, or null. Only a direct child of
    /// <c>files/</c> counts: something further in is part of a trashed folder,
    /// and only the folder as a whole has a record of where it came from.
    /// </summary>
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

    // ── where the trash lives ────────────────────────────────────────────

    private static string HomeTrash() => Path.Combine(XdgPaths.DataHome, "Trash");

    /// <summary>
    /// Picks the trash an item belongs in, and tells the caller which disk it
    /// is at the top of (null for the home trash, whose records are absolute).
    /// </summary>
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

    /// <summary>
    /// The two places the spec allows on a disk that is not the home one: a
    /// shared <c>.Trash</c> the administrator set up, which must be sticky and
    /// a real directory, and otherwise a private <c>.Trash-1000</c> of our own.
    /// </summary>
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
                return false; // a symlink here is the classic way to get a trash redirected somewhere it should not be
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

    /// <summary>
    /// The mount point a path sits on: the deepest mounted directory it lives
    /// under. Two paths with the same one can be moved between without a copy.
    /// </summary>
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

    /// <summary>The trash's own disk, for reading records stored relative to it.</summary>
    private static string? TopDirectoryOfTrash(string trash)
    {
        // ".../.Trash-1000" and ".../.Trash/1000" both sit at the top of the
        // disk they serve; the home trash keeps absolute paths and needs none.
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

    // ── odds and ends ────────────────────────────────────────────────────

    /// <summary>Stored the way a URL stores a path: each part escaped, the slashes left alone.</summary>
    internal static string EncodePath(string path) =>
        string.Join('/', path.Split('/').Select(Uri.EscapeDataString));

    internal static string DecodePath(string encoded) => Uri.UnescapeDataString(encoded);

    private static bool IsUnderOrEqual(string path, string directory) =>
        PathCompare.PathMatches(path, directory)
        || path.StartsWith(
            directory.EndsWith(Path.DirectorySeparatorChar) ? directory : directory + Path.DirectorySeparatorChar,
            PathCompare.Comparison);

    private static void Move(string from, string to)
    {
        if (Directory.Exists(from))
            Directory.Move(from, to);
        else
            File.Move(from, to);
    }

    private static bool TryCreate(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void TryDelete(string file)
    {
        try
        {
            File.Delete(file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
