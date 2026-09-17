using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Rove.Core.Protocol;

namespace Rove.Core.Services;

/// <summary>
/// Reading a zip as though it were a folder. A zip does not really have
/// folders in it — it has a flat list of entries with slashes in their names —
/// so the folders a listing shows are worked out from those names rather than
/// read out of the file. An archive written without a single directory entry
/// in it, which is common, browses exactly like one written with them.
///
/// <para>
/// Nothing here writes into an archive. Rove browses one and takes things out
/// of it; putting things back in is what <see cref="ArchiveService"/> does,
/// and it does it by making a new zip rather than editing one.
/// </para>
/// </summary>
public static class ArchiveBrowser
{
    /// <summary>Where a file opened out of a zip is put so a program can read it.</summary>
    private const string TempFolderName = "rove-archives";

    /// <summary>
    /// Everything sitting directly at <paramref name="location"/> — the files
    /// whose names have no slash left in them once this place is taken off
    /// the front, and one folder for each distinct name that still does.
    /// </summary>
    public static FolderItem[] Children(ArchivePath location)
    {
        using ZipArchive zip = ZipFile.OpenRead(LongPath.ForIo(location.Archive));
        string prefix = location.IsRoot ? string.Empty : location.Entry + '/';
        DateTime fallback = ArchiveWriteTime(location.Archive);

        Dictionary<string, FolderItem> children = new(PathCompare.Comparer);
        foreach (ZipArchiveEntry entry in zip.Entries)
        {
            string name = entry.FullName.Replace('\\', '/');
            if (!ArchivePath.IsSafeEntryName(name) || !name.StartsWith(prefix, PathCompare.Comparison))
                continue;

            string rest = name[prefix.Length..];
            int slash = rest.IndexOf('/');
            if (slash < 0)
            {
                if (rest.Length > 0)
                    children.TryAdd(rest, FileEntry(location.Archive, entry, name));
                continue;
            }

            string folder = rest[..slash];
            if (folder.Length == 0)
                continue;

            // "sub/" is the folder itself written down, and is the only entry
            // that can say when it was last touched — so it replaces the
            // stand-in a file underneath it would otherwise have left here.
            FolderItem item = Folder(location.Down(folder), rest.Length == slash + 1 ? entry.LastWriteTime.LocalDateTime : fallback);
            if (rest.Length == slash + 1)
                children[folder] = item;
            else
                children.TryAdd(folder, item);
        }
        return [.. children.Values];
    }

    /// <summary>
    /// What sits at <paramref name="location"/>, or null when nothing does —
    /// the answer the path bar needs before it will go somewhere.
    /// </summary>
    public static FolderItem? Describe(ArchivePath location)
    {
        if (location.IsRoot)
            return Folder(location, ArchiveWriteTime(location.Archive));

        using ZipArchive zip = ZipFile.OpenRead(LongPath.ForIo(location.Archive));
        string prefix = location.Entry + '/';
        foreach (ZipArchiveEntry entry in zip.Entries)
        {
            string name = entry.FullName.Replace('\\', '/');
            if (!ArchivePath.IsSafeEntryName(name))
                continue;
            if (name.StartsWith(prefix, PathCompare.Comparison))
                return Folder(location, ArchiveWriteTime(location.Archive));
            if (PathCompare.Comparer.Equals(name, location.Entry))
                return FileEntry(location.Archive, entry, name);
        }
        return null;
    }

    /// <summary>A folder inside an archive, described without opening it.</summary>
    public static FolderItem Folder(ArchivePath location) =>
        Folder(location, ArchiveWriteTime(location.Archive));

    private static FolderItem Folder(ArchivePath location, DateTime modified) =>
        new(location.Name, location.FullPath, FileAttributes.Directory, modified, true, null, string.Empty);

    private static FolderItem FileEntry(string archive, ZipArchiveEntry entry, string entryName)
    {
        ArchivePath at = new(archive, ArchivePath.Trim(entryName));
        return new(
            at.Name,
            at.FullPath,
            FileAttributes.Normal,
            entry.LastWriteTime.LocalDateTime,
            false,
            entry.Length,
            Path.GetExtension(at.Name));
    }

    /// <summary>
    /// Puts one entry somewhere a program can open it, and says where that
    /// is. A zip is not a folder a program can be pointed at, so opening
    /// something inside one means taking a copy out first — the same copy
    /// each time, replaced whenever the archive it came from is newer than
    /// it.
    ///
    /// <para>
    /// The copy is exactly that: nothing written to it goes back into the
    /// zip. Whoever calls this is expected to say so.
    /// </para>
    /// </summary>
    public static string CopyOut(ArchivePath location)
    {
        if (location.IsRoot)
            return LongPath.Display(location.Archive);

        using ZipArchive zip = ZipFile.OpenRead(LongPath.ForIo(location.Archive));
        ZipArchiveEntry entry = Find(zip, location.Entry)
            ?? throw new FileNotFoundException($"{location.Name} is not in this zip.", location.FullPath);

        string folder = Path.Combine(Path.GetTempPath(), TempFolderName, Fingerprint(location.Archive));
        string target = ArchiveService.EntryPathInside(folder, location.Entry)
            ?? throw new IOException($"{location.Name} cannot be written to a folder of its own.");

        if (Path.GetDirectoryName(target) is { Length: > 0 } parent)
            Directory.CreateDirectory(LongPath.ForIo(parent));

        if (IsStale(target, location.Archive))
            entry.ExtractToFile(LongPath.ForIo(target), overwrite: true);
        return target;
    }

    private static ZipArchiveEntry? Find(ZipArchive zip, string entryName)
    {
        foreach (ZipArchiveEntry entry in zip.Entries)
        {
            if (PathCompare.Comparer.Equals(entry.FullName.Replace('\\', '/'), entryName))
                return entry;
        }
        return null;
    }

    private static bool IsStale(string copy, string archive)
    {
        try
        {
            string io = LongPath.ForIo(copy);
            return !File.Exists(io)
                || File.GetLastWriteTimeUtc(io) < File.GetLastWriteTimeUtc(LongPath.ForIo(archive));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return true;
        }
    }

    private static DateTime ArchiveWriteTime(string archive)
    {
        try
        {
            return File.GetLastWriteTime(LongPath.ForIo(archive));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return DateTime.MinValue;
        }
    }

    /// <summary>
    /// A short, stable, filename-safe stand-in for an archive's path, so two
    /// zips of the same name from different folders never share a copy.
    /// </summary>
    private static string Fingerprint(string archive)
    {
        string key = LongPath.Display(archive);
        if (PathCompare.Comparison == StringComparison.OrdinalIgnoreCase)
            key = key.ToUpperInvariant();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..16];
    }
}
