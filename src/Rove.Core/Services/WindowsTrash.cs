using System.Buffers.Binary;
using System.Runtime.Versioning;
using System.Text;

namespace Rove.Core.Services;

/// <summary>
/// The Windows Recycle Bin, read the way it is actually stored. A deleted
/// item does not go anywhere clever: it is moved to <c>$Recycle.Bin</c> at the
/// top of its own volume, under a folder named for the account that deleted
/// it, as a pair of files. <c>$R…</c> is the item itself; <c>$I…</c> beside it
/// is a small record saying where it came from, how big it was and when it
/// went — the same arrangement the Linux trash uses, under different names.
///
/// <para>
/// Deleting still goes through the shell, which is the only way to get an
/// entry the Recycle Bin recognises. Putting one back is this: find the
/// record naming the path, move the item to it, drop the record.
/// </para>
/// </summary>
[SupportedOSPlatform("windows")]
internal static class WindowsTrash
{
    private const string BinFolderName = "$Recycle.Bin";
    private const string RecordPrefix = "$I";
    private const string ItemPrefix = "$R";

    /// <summary>Version 1 keeps a fixed 260-character path; version 2 writes its length first.</summary>
    private const long LegacyVersion = 1;

    private const int HeaderLength = 24;
    private const int LegacyPathChars = 260;

    /// <summary>
    /// Moves items back to where they were deleted from, and returns the ones
    /// that actually made it. Anything already emptied out of the bin, or
    /// whose old name is taken again, is simply not among them.
    /// </summary>
    public static string[] Restore(string[] paths) =>
        Restore(paths, BinRootsFor(paths));

    internal static string[] Restore(string[] paths, IEnumerable<string> binRoots)
    {
        List<string> roots = [.. binRoots];
        if (roots.Count == 0)
            return [];

        List<string> restored = [];
        foreach (string path in paths)
        {
            string full = Path.GetFullPath(LongPath.Display(path));
            if (Find(roots, full) is not { } found)
                continue;
            if (RestoreOne(found.ItemPath, found.RecordPath, full))
                restored.Add(path);
        }
        return [.. restored];
    }

    private static bool RestoreOne(string itemPath, string recordPath, string destination)
    {
        string item = LongPath.ForIo(itemPath);
        string target = LongPath.ForIo(destination);

        if (File.Exists(target) || Directory.Exists(target))
            return false; // something took the name back; do not clobber it

        try
        {
            if (Path.GetDirectoryName(destination) is { Length: > 0 } parent)
                Directory.CreateDirectory(LongPath.ForIo(parent));

            if (Directory.Exists(item))
                Directory.Move(item, target);
            else
                File.Move(item, target);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }

        TryDelete(recordPath);
        return true;
    }

    /// <summary>The record naming <paramref name="original"/>, and the item it belongs to.</summary>
    private static (string ItemPath, string RecordPath)? Find(IEnumerable<string> binRoots, string original)
    {
        foreach (string root in binRoots)
        {
            foreach (string userFolder in Subdirectories(root))
            {
                foreach (string record in RecordsIn(userFolder))
                {
                    if (ReadRecordedPath(record) is not { } recorded)
                        continue;
                    if (!PathCompare.PathMatches(recorded, original))
                        continue;

                    string item = ItemFor(record);
                    if (File.Exists(LongPath.ForIo(item)) || Directory.Exists(LongPath.ForIo(item)))
                        return (item, record);
                }
            }
        }
        return null;
    }

    /// <summary>$I2AB3CD.txt describes $R2AB3CD.txt beside it.</summary>
    private static string ItemFor(string recordPath)
    {
        string name = Path.GetFileName(recordPath);
        string directory = Path.GetDirectoryName(recordPath) ?? string.Empty;
        return Path.Combine(directory, ItemPrefix + name[RecordPrefix.Length..]);
    }

    /// <summary>
    /// The path a record names, or null when the file is not one — the bin
    /// holds other bookkeeping, and a half-written record is possible too.
    /// </summary>
    internal static string? ReadRecordedPath(string recordPath)
    {
        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(LongPath.ForIo(recordPath));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        if (bytes.Length < HeaderLength)
            return null;

        long version = BinaryPrimitives.ReadInt64LittleEndian(bytes);
        ReadOnlySpan<byte> text;

        if (version == LegacyVersion)
        {
            if (bytes.Length < HeaderLength + (LegacyPathChars * 2))
                return null;
            text = bytes.AsSpan(HeaderLength, LegacyPathChars * 2);
        }
        else
        {
            if (bytes.Length < HeaderLength + 4)
                return null;
            int chars = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(HeaderLength));
            int start = HeaderLength + 4;
            if (chars <= 0 || start + (chars * 2) > bytes.Length)
                return null;
            text = bytes.AsSpan(start, chars * 2);
        }

        string path = Encoding.Unicode.GetString(text);
        int end = path.IndexOf('\0');
        if (end >= 0)
            path = path[..end];
        return path.Length == 0 ? null : path;
    }

    // ── where the bins are ───────────────────────────────────────────────

    /// <summary>
    /// One <c>$Recycle.Bin</c> per volume: an item is only ever moved, never
    /// copied, so it is still on the disk it was deleted from.
    /// </summary>
    private static IEnumerable<string> BinRootsFor(IEnumerable<string> paths)
    {
        HashSet<string> roots = new(PathCompare.Comparer);
        foreach (string path in paths)
        {
            try
            {
                if (Path.GetPathRoot(Path.GetFullPath(LongPath.Display(path))) is { Length: > 0 } volume)
                    roots.Add(Path.Combine(volume, BinFolderName));
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
            }
        }
        return roots;
    }

    private static IEnumerable<string> Subdirectories(string path)
    {
        try
        {
            return Directory.Exists(LongPath.ForIo(path))
                ? Directory.EnumerateDirectories(LongPath.ForIo(path))
                : [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return []; // another account's corner of the bin
        }
    }

    private static IEnumerable<string> RecordsIn(string userFolder)
    {
        try
        {
            return Directory.EnumerateFiles(userFolder, RecordPrefix + "*");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(LongPath.ForIo(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
