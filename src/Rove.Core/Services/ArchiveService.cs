using System.IO.Compression;

namespace Rove.Core.Services;

/// <summary>
/// Zip files, both ways. Unpacking puts an archive into a folder of its own,
/// and nothing is ever written outside that folder: an entry whose name
/// climbs back out of it ("../../.bashrc") is refused rather than followed.
/// Packing goes the other way — a set of items beside each other becomes one
/// zip beside them, under a name nothing else has.
/// </summary>
public static class ArchiveService
{
    /// <summary>How many "name (2)", "name (3)" tries before giving up.</summary>
    private const int MaxNameAttempts = 1024;

    /// <summary>What a zip Rove makes is called.</summary>
    public const string ArchiveExtension = ".zip";

    private static readonly string[] _extensions = [ArchiveExtension];

    /// <summary>True when Rove knows how to unpack this file.</summary>
    public static bool IsArchive(string path) =>
        _extensions.Contains(Path.GetExtension(LongPath.Display(path)), StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// A folder beside the archive, named after it, that does not exist yet —
    /// null when even the numbered names are all taken.
    /// </summary>
    public static string? FreeDestination(string targetDirectory, string archivePath)
    {
        string stem = Path.GetFileNameWithoutExtension(LongPath.Display(archivePath));
        if (stem.Length == 0)
            stem = "extracted";
        return FreeName(targetDirectory, stem, string.Empty);
    }

    /// <summary>A zip beside the items, named after them, that does not exist yet.</summary>
    public static string? FreeArchive(string targetDirectory, string stem) =>
        FreeName(targetDirectory, stem.Length == 0 ? "archive" : stem, ArchiveExtension);

    /// <summary>
    /// What to call a zip made of these items: the item's own name when there
    /// is one of them, and the folder they are sitting in when there are
    /// several — the same answer every other file manager gives.
    /// </summary>
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

    /// <summary>Number of files (not folders) the archive holds.</summary>
    public static int CountFiles(string archivePath)
    {
        using ZipArchive zip = ZipFile.OpenRead(LongPath.ForIo(archivePath));
        return zip.Entries.Count(e => !IsDirectoryEntry(e));
    }

    /// <summary>
    /// Unpacks every entry into <paramref name="destination"/>, which is
    /// created here and is the caller's to clean up if this throws.
    /// </summary>
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

    /// <summary>
    /// Packs every item into one new zip at <paramref name="destination"/>,
    /// each keeping its own name at the top of the archive. The file is
    /// created here and is the caller's to clean up if this throws.
    /// </summary>
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

    /// <summary>
    /// Adds a folder and everything under it. A link to another folder is
    /// noted and not followed — a link that points back up its own tree would
    /// otherwise be packed forever.
    /// </summary>
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

        // An empty folder is still part of what the user picked, and a zip
        // only keeps one if it is written down as an entry of its own.
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
        // Zip cannot hold a date before 1980, and refuses one loudly.
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

    /// <summary>
    /// Where an entry lands, or null when its name points anywhere but inside
    /// the destination — zips can carry absolute paths and "..", and a written
    /// name is not the same thing as a permitted one.
    /// </summary>
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
