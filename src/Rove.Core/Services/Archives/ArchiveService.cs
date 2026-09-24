using System.Formats.Tar;
using System.IO.Compression;

namespace Rove.Core.Services;

public static class ArchiveService
{
    private const int MaxNameAttempts = 1024;

    public const string ArchiveExtension = ".zip";
    public const string TarExtension = ".tar";
    public const string TarGzExtension = ".tar.gz";
    private const string TgzExtension = ".tgz";

    internal enum Kind { Zip, Tar, TarGz }

    internal static readonly string[] Extensions = [ArchiveExtension, TarGzExtension, TgzExtension, TarExtension];

    public static bool IsArchive(string path)
    {
        string display = LongPath.Display(path);
        return Extensions.Any(ext => display.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
    }

    internal static Kind KindOf(string archivePath)
    {
        string display = LongPath.Display(archivePath);
        if (display.EndsWith(TarGzExtension, StringComparison.OrdinalIgnoreCase)
            || display.EndsWith(TgzExtension, StringComparison.OrdinalIgnoreCase))
            return Kind.TarGz;
        return display.EndsWith(TarExtension, StringComparison.OrdinalIgnoreCase) ? Kind.Tar : Kind.Zip;
    }

    public static string? FreeDestination(string targetDirectory, string archivePath)
    {
        string stem = Path.GetFileNameWithoutExtension(LongPath.Display(archivePath));
        if (KindOf(archivePath) == Kind.TarGz && stem.EndsWith(".tar", StringComparison.OrdinalIgnoreCase))
            stem = stem[..^".tar".Length];
        if (stem.Length == 0)
            stem = "extracted";
        return FreeName(targetDirectory, stem, string.Empty);
    }

    public static string? FreeArchive(string targetDirectory, string stem, ArchiveFormat format = ArchiveFormat.Zip) =>
        FreeName(targetDirectory, stem.Length == 0 ? "archive" : stem, ExtensionFor(format));

    public static string ExtensionFor(ArchiveFormat format) =>
        format == ArchiveFormat.TarGz ? TarGzExtension : ArchiveExtension;

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
        switch (KindOf(archivePath))
        {
            case Kind.Tar:
                ExtractTar(archivePath, destination, verb, ticker, index, gzip: false, ct);
                break;
            case Kind.TarGz:
                ExtractTar(archivePath, destination, verb, ticker, index, gzip: true, ct);
                break;
            default:
                ExtractZip(archivePath, destination, verb, ticker, index, ct);
                break;
        }
    }

    private static void ExtractZip(
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

    private static void ExtractTar(
        string archivePath, string destination, string verb,
        ProgressTicker ticker, int index, bool gzip, CancellationToken ct
    )
    {
        Directory.CreateDirectory(LongPath.ForIo(destination));

        using FileStream file = new(LongPath.ForIo(archivePath), FileMode.Open, FileAccess.Read, FileShare.Read);
        using Stream content = gzip ? new GZipStream(file, CompressionMode.Decompress) : file;
        using TarReader reader = new(content);

        TarEntry? entry;
        while ((entry = reader.GetNextEntry()) is not null)
        {
            ct.ThrowIfCancellationRequested();

            if (entry.EntryType is not (TarEntryType.Directory or TarEntryType.RegularFile or TarEntryType.V7RegularFile))
                continue;

            if (EntryPathInside(destination, entry.Name) is not { } target)
                throw new IOException($"{entry.Name} would be written outside the folder it is being unpacked into.");

            if (entry.EntryType == TarEntryType.Directory)
            {
                Directory.CreateDirectory(LongPath.ForIo(target));
                continue;
            }

            if (Path.GetDirectoryName(target) is { Length: > 0 } parent)
                Directory.CreateDirectory(LongPath.ForIo(parent));

            ticker.Report(verb, index, -1, Path.GetFileName(entry.Name));
            entry.ExtractToFile(LongPath.ForIo(target), overwrite: false);
        }
    }

    internal static void Compress(
        IReadOnlyList<string> paths, string destination, string verb,
        ProgressTicker ticker, CancellationToken ct
    )
    {
        if (KindOf(destination) == Kind.TarGz)
            CompressTarGz(paths, destination, verb, ticker, ct);
        else
            CompressZip(paths, destination, verb, ticker, ct);
    }

    private static void CompressZip(
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

    private static void CompressTarGz(
        IReadOnlyList<string> paths, string destination, string verb,
        ProgressTicker ticker, CancellationToken ct
    )
    {
        using FileStream file = new(LongPath.ForIo(destination), FileMode.CreateNew, FileAccess.Write);
        using GZipStream gzip = new(file, CompressionLevel.Optimal);
        using TarWriter writer = new(gzip);

        for (int i = 0; i < paths.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            string source = LongPath.ForIo(paths[i]);
            string name = Path.GetFileName(Path.TrimEndingDirectorySeparator(LongPath.Display(paths[i])));
            if (name.Length == 0)
                continue;

            if (Directory.Exists(source))
                AddFolderTar(writer, source, name, verb, ticker, i, ct);
            else if (File.Exists(source))
                AddFileTar(writer, source, name, verb, ticker, i, ct);
            else
                throw new FileNotFoundException($"No such item: {LongPath.Display(paths[i])}", source);
        }
    }

    private static void AddFolderTar(
        TarWriter writer, string source, string entryName, string verb,
        ProgressTicker ticker, int index, CancellationToken ct
    )
    {
        ct.ThrowIfCancellationRequested();
        writer.WriteEntry(source, entryName);

        DirectoryInfo directory = new(source);
        foreach (FileInfo file in directory.EnumerateFiles())
            AddFileTar(writer, file.FullName, $"{entryName}/{file.Name}", verb, ticker, index, ct);

        foreach (DirectoryInfo sub in directory.EnumerateDirectories())
        {
            if (sub.LinkTarget is not null)
                continue;
            AddFolderTar(writer, sub.FullName, $"{entryName}/{sub.Name}", verb, ticker, index, ct);
        }
    }

    private static void AddFileTar(
        TarWriter writer, string source, string entryName, string verb,
        ProgressTicker ticker, int index, CancellationToken ct
    )
    {
        ct.ThrowIfCancellationRequested();
        ticker.Report(verb, index, -1, Path.GetFileName(entryName));
        writer.WriteEntry(source, entryName);
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
