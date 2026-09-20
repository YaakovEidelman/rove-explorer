using System.IO.Compression;

namespace Rove.Core.Services;

public static class ArchiveService
{
    private const int MaxNameAttempts = 1024;

    public const string ArchiveExtension = ".zip";

    private static readonly string[] _extensions = [ArchiveExtension];

    public static bool IsArchive(string path) =>
        _extensions.Contains(Path.GetExtension(LongPath.Display(path)), StringComparer.OrdinalIgnoreCase);

    public static string? FreeDestination(string targetDirectory, string archivePath)
    {
        string stem = Path.GetFileNameWithoutExtension(LongPath.Display(archivePath));
        if (stem.Length == 0)
            stem = "extracted";
        return FreeName(targetDirectory, stem, string.Empty);
    }

    public static string? FreeArchive(string targetDirectory, string stem) =>
        FreeName(targetDirectory, stem.Length == 0 ? "archive" : stem, ArchiveExtension);

    public static string ArchiveStem(IReadOnlyList<string> paths, string targetDirectory)
    {
        if (paths.Count == 1)
        {
            string only = Path.GetFileName(Path.TrimEndingDirectorySeparator(LongPath.Display(paths[0])));
            if (only.Length > 0)
                return Directory.Exists(LongPath.ForIo(paths[0])) ? only : Path.GetFileNameWithoutExtension(only);
        }

        string folder = Path.GetFileName(Path.TrimEndingDirectorySeparator(LongPath.Display(targetDirectory)));
        return folder.Length > 0 ? folder : "archive";
    }

    private static string? FreeName(string targetDirectory, string stem, string extension)
    {
        string directory = LongPath.Display(targetDirectory);
        for (int attempt = 0; attempt < MaxNameAttempts; attempt++)
        {
            string bare = attempt == 0 ? stem : $"{stem} ({attempt + 1})";
            string candidate = Path.Combine(directory, bare + extension);
            string io = LongPath.ForIo(candidate);
            if (!File.Exists(io) && !Directory.Exists(io))
                return candidate;
        }
        return null;
    }

    public static int CountFiles(string archivePath)
    {
        using ZipArchive zip = ZipFile.OpenRead(LongPath.ForIo(archivePath));
        return zip.Entries.Count(e => !IsDirectoryEntry(e));
    }

    internal static void Extract(
        string archivePath, string destination, string verb,
        ProgressTicker ticker, int index, CancellationToken ct
    )
    {
        using ZipArchive zip = ZipFile.OpenRead(LongPath.ForIo(archivePath));
        Directory.CreateDirectory(LongPath.ForIo(destination));

        foreach (ZipArchiveEntry entry in zip.Entries)
        {
            ct.ThrowIfCancellationRequested();

            if (EntryPathInside(destination, entry.FullName) is not { } target)
                throw new IOException($"{entry.FullName} would be written outside the folder it is being unpacked into.");

            if (IsDirectoryEntry(entry))
            {
                Directory.CreateDirectory(LongPath.ForIo(target));
                continue;
            }

            if (Path.GetDirectoryName(target) is { Length: > 0 } parent)
                Directory.CreateDirectory(LongPath.ForIo(parent));

            ticker.Report(verb, index, -1, entry.Name);
            entry.ExtractToFile(LongPath.ForIo(target), overwrite: false);
        }
    }

    internal static void Compress(
        IReadOnlyList<string> paths, string destination, string verb,
        ProgressTicker ticker, CancellationToken ct
    )
    {
        using FileStream file = new(LongPath.ForIo(destination), FileMode.CreateNew, FileAccess.Write);
        using ZipArchive zip = new(file, ZipArchiveMode.Create);

        for (int i = 0; i < paths.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            string source = LongPath.ForIo(paths[i]);
            string name = Path.GetFileName(Path.TrimEndingDirectorySeparator(LongPath.Display(paths[i])));
            if (name.Length == 0)
                continue;

            if (Directory.Exists(source))
                AddFolder(zip, source, name, verb, ticker, i, ct);
            else if (File.Exists(source))
                AddFile(zip, source, name, verb, ticker, i, ct);
            else
                throw new FileNotFoundException($"No such item: {LongPath.Display(paths[i])}", source);
        }
    }

    private static void AddFolder(
        ZipArchive zip, string source, string prefix, string verb,
        ProgressTicker ticker, int index, CancellationToken ct
    )
    {
        ct.ThrowIfCancellationRequested();
        DirectoryInfo directory = new(source);
        bool empty = true;

        foreach (FileInfo file in directory.EnumerateFiles())
        {
            empty = false;
            AddFile(zip, file.FullName, $"{prefix}/{file.Name}", verb, ticker, index, ct);
        }

        foreach (DirectoryInfo sub in directory.EnumerateDirectories())
        {
            empty = false;
            if (sub.LinkTarget is not null)
                continue;
            AddFolder(zip, sub.FullName, $"{prefix}/{sub.Name}", verb, ticker, index, ct);
        }

        if (empty)
            zip.CreateEntry($"{prefix}/");
    }

    private static void AddFile(
        ZipArchive zip, string source, string entryName, string verb,
        ProgressTicker ticker, int index, CancellationToken ct
    )
    {
        ct.ThrowIfCancellationRequested();
        ticker.Report(verb, index, -1, Path.GetFileName(entryName));

        ZipArchiveEntry entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
        DateTime modified = File.GetLastWriteTime(source);
        if (modified.Year >= 1980)
            entry.LastWriteTime = modified;

        using Stream target = entry.Open();
        using FileStream input = new(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        input.CopyTo(target);
    }

    private static bool IsDirectoryEntry(ZipArchiveEntry entry) =>
        entry.Name.Length == 0
        || entry.FullName.EndsWith('/')
        || entry.FullName.EndsWith('\\');

    internal static string? EntryPathInside(string destination, string entryName)
    {
        if (entryName.Length == 0)
            return null;

        string relative = entryName
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);

        string root = Path.GetFullPath(LongPath.Display(destination));
        string full;
        try
        {
            full = Path.GetFullPath(Path.Combine(root, relative));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }

        string fence = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        return full.StartsWith(fence, PathCompare.Comparison) ? full : null;
    }
}
