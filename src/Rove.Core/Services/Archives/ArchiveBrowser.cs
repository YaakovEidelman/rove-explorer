using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Rove.Core.Protocol;

namespace Rove.Core.Services;

public static class ArchiveBrowser
{
    private const string TempFolderName = "rove-archives";

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

            FolderItem item = Folder(location.Down(folder), rest.Length == slash + 1 ? entry.LastWriteTime.LocalDateTime : fallback);
            if (rest.Length == slash + 1)
                children[folder] = item;
            else
                children.TryAdd(folder, item);
        }
        return [.. children.Values];
    }

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

    private static string Fingerprint(string archive)
    {
        string key = LongPath.Display(archive);
        if (PathCompare.Comparison == StringComparison.OrdinalIgnoreCase)
            key = key.ToUpperInvariant();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..16];
    }
}
