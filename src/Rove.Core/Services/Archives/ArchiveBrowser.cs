using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Rove.Core.Protocol;

namespace Rove.Core.Services;

public static class ArchiveBrowser
{
    private const string TempFolderName = "rove-archives";

    private readonly record struct Entry(string Name, bool IsDirectory, long Length, DateTime Modified);

    public static FolderItem[] Children(ArchivePath location)
    {
        string prefix = location.IsRoot ? string.Empty : location.Entry + '/';
        DateTime fallback = ArchiveWriteTime(location.Archive);

        Dictionary<string, FolderItem> children = new(PathCompare.Comparer);
        foreach (Entry entry in ReadEntries(location.Archive))
        {
            if (!ArchivePath.IsSafeEntryName(entry.Name) || !entry.Name.StartsWith(prefix, PathCompare.Comparison))
                continue;

            string rest = entry.Name[prefix.Length..];
            int slash = rest.IndexOf('/');
            if (slash < 0)
            {
                if (rest.Length > 0)
                    children.TryAdd(rest, FileEntry(location.Archive, entry));
                continue;
            }

            string folder = rest[..slash];
            if (folder.Length == 0)
                continue;

            FolderItem item = Folder(location.Down(folder), rest.Length == slash + 1 ? entry.Modified : fallback);
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

        string prefix = location.Entry + '/';
        foreach (Entry entry in ReadEntries(location.Archive))
        {
            if (!ArchivePath.IsSafeEntryName(entry.Name))
                continue;
            if (entry.Name.StartsWith(prefix, PathCompare.Comparison))
                return Folder(location, ArchiveWriteTime(location.Archive));
            if (PathCompare.Comparer.Equals(entry.Name, location.Entry))
                return FileEntry(location.Archive, entry);
        }
        return null;
    }

    public static FolderItem Folder(ArchivePath location) =>
        Folder(location, ArchiveWriteTime(location.Archive));

    private static FolderItem Folder(ArchivePath location, DateTime modified) =>
        new(location.Name, location.FullPath, FileAttributes.Directory, modified, true, null, string.Empty);

    private static FolderItem FileEntry(string archive, Entry entry)
    {
        ArchivePath at = new(archive, ArchivePath.Trim(entry.Name));
        return new(at.Name, at.FullPath, FileAttributes.Normal, entry.Modified, false, entry.Length, Path.GetExtension(at.Name));
    }

    public static string CopyOut(ArchivePath location)
    {
        if (location.IsRoot)
            return LongPath.Display(location.Archive);

        string folder = Path.Combine(Path.GetTempPath(), TempFolderName, Fingerprint(location.Archive));
        string target = ArchiveService.EntryPathInside(folder, location.Entry)
            ?? throw new IOException($"{location.Name} cannot be written to a folder of its own.");

        if (Path.GetDirectoryName(target) is { Length: > 0 } parent)
            Directory.CreateDirectory(LongPath.ForIo(parent));

        if (IsStale(target, location.Archive))
            ExtractSingleEntry(location, target);
        return target;
    }

    private static void ExtractSingleEntry(ArchivePath location, string target)
    {
        if (ArchiveService.KindOf(location.Archive) == ArchiveService.Kind.Zip)
        {
            using ZipArchive zip = ZipFile.OpenRead(LongPath.ForIo(location.Archive));
            ZipArchiveEntry entry = FindZipEntry(zip, location.Entry)
                ?? throw new FileNotFoundException($"{location.Name} is not in this archive.", location.FullPath);
            entry.ExtractToFile(LongPath.ForIo(target), overwrite: true);
            return;
        }

        bool gzip = ArchiveService.KindOf(location.Archive) == ArchiveService.Kind.TarGz;
        using FileStream file = new(LongPath.ForIo(location.Archive), FileMode.Open, FileAccess.Read, FileShare.Read);
        using Stream content = gzip ? new GZipStream(file, CompressionMode.Decompress) : file;
        using TarReader reader = new(content);

        TarEntry? entry2;
        while ((entry2 = reader.GetNextEntry()) is not null)
        {
            if (entry2.EntryType is TarEntryType.RegularFile or TarEntryType.V7RegularFile
                && PathCompare.Comparer.Equals(NormalizeName(entry2), location.Entry))
            {
                entry2.ExtractToFile(LongPath.ForIo(target), overwrite: true);
                return;
            }
        }
        throw new FileNotFoundException($"{location.Name} is not in this archive.", location.FullPath);
    }

    private static ZipArchiveEntry? FindZipEntry(ZipArchive zip, string entryName)
    {
        foreach (ZipArchiveEntry entry in zip.Entries)
        {
            if (PathCompare.Comparer.Equals(entry.FullName.Replace('\\', '/'), entryName))
                return entry;
        }
        return null;
    }

    private static IEnumerable<Entry> ReadEntries(string archivePath) =>
        ArchiveService.KindOf(archivePath) switch
        {
            ArchiveService.Kind.Zip => ReadZipEntries(archivePath),
            ArchiveService.Kind.Tar => ReadTarEntries(archivePath, gzip: false),
            _ => ReadTarEntries(archivePath, gzip: true),
        };

    private static IEnumerable<Entry> ReadZipEntries(string archivePath)
    {
        using ZipArchive zip = ZipFile.OpenRead(LongPath.ForIo(archivePath));
        foreach (ZipArchiveEntry entry in zip.Entries)
        {
            string name = entry.FullName.Replace('\\', '/');
            bool isDirectory = entry.Name.Length == 0 || name.EndsWith('/');
            yield return new Entry(name, isDirectory, isDirectory ? 0 : entry.Length, entry.LastWriteTime.LocalDateTime);
        }
    }

    private static IEnumerable<Entry> ReadTarEntries(string archivePath, bool gzip)
    {
        using FileStream file = new(LongPath.ForIo(archivePath), FileMode.Open, FileAccess.Read, FileShare.Read);
        using Stream content = gzip ? new GZipStream(file, CompressionMode.Decompress) : file;
        using TarReader reader = new(content);

        TarEntry? entry;
        while ((entry = reader.GetNextEntry()) is not null)
        {
            if (entry.EntryType is not (TarEntryType.Directory or TarEntryType.RegularFile or TarEntryType.V7RegularFile))
                continue;

            bool isDirectory = entry.EntryType == TarEntryType.Directory;
            yield return new Entry(NormalizeName(entry), isDirectory, isDirectory ? 0 : entry.Length, entry.ModificationTime.LocalDateTime);
        }
    }

    private static string NormalizeName(TarEntry entry)
    {
        string name = entry.Name.Replace('\\', '/');
        if (entry.EntryType == TarEntryType.Directory && !name.EndsWith('/'))
            name += "/";
        return name;
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
