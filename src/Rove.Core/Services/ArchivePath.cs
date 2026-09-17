namespace Rove.Core.Services;

/// <summary>
/// A place inside a zip, written the way a place on disk is written: the
/// archive's own path, and then the way on through it —
/// <c>C:\downloads\photos.zip\summer\beach.jpg</c>. One kind of path means the
/// path bar, the crumbs, the completions and the bookmarks all keep working
/// inside an archive without knowing that is where they are; only the layer
/// that actually reads a folder has to tell the two apart.
///
/// <para>
/// <see cref="Entry"/> is written the way a zip writes its own entry names —
/// forward slashes, no slash at either end. Empty means the top of the
/// archive.
/// </para>
/// </summary>
public readonly record struct ArchivePath(string Archive, string Entry)
{
    private static readonly char[] _separators =
        [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];

    /// <summary>True when this points at the archive itself rather than into it.</summary>
    public bool IsRoot => Entry.Length == 0;

    /// <summary>The path as it is shown and typed, in this system's separators.</summary>
    public string FullPath => PathOf(Entry);

    /// <summary>The name of this place — the entry's own last step, or the zip's filename.</summary>
    public string Name
    {
        get
        {
            if (Entry.Length == 0)
                return Path.GetFileName(Path.TrimEndingDirectorySeparator(LongPath.Display(Archive)));
            int cut = Entry.LastIndexOf('/');
            return cut < 0 ? Entry : Entry[(cut + 1)..];
        }
    }

    /// <summary>
    /// One step out, while that is still inside the archive. At the top there
    /// is nowhere further to go without leaving, so this is null and the
    /// caller goes to the folder the zip file sits in.
    /// </summary>
    public ArchivePath? Parent
    {
        get
        {
            if (Entry.Length == 0)
                return null;
            int cut = Entry.LastIndexOf('/');
            return new ArchivePath(Archive, cut < 0 ? string.Empty : Entry[..cut]);
        }
    }

    /// <summary>Where an entry of this archive lives, as a path the rest of Rove can carry.</summary>
    public string PathOf(string entryName)
    {
        string archive = LongPath.Display(Archive);
        string trimmed = Trim(entryName);
        return trimmed.Length == 0
            ? archive
            : archive + Path.DirectorySeparatorChar + trimmed.Replace('/', Path.DirectorySeparatorChar);
    }

    /// <summary>The place one step down from here, by the name of a child.</summary>
    public ArchivePath Down(string childName) =>
        new(Archive, Entry.Length == 0 ? Trim(childName) : Entry + '/' + Trim(childName));

    /// <summary>True when <paramref name="path"/> names something inside a zip.</summary>
    public static bool IsInside(string? path) => TryParse(path, out _);

    /// <summary>
    /// Splits a path into the archive it goes through and the way on through
    /// it, or says it does not go through one at all.
    ///
    /// <para>
    /// The archive is the <em>first</em> step of the path that is a zip file
    /// really sitting on disk. Reading left to right matters: a zip inside a
    /// zip is not a folder anything can open, so the outer one is always the
    /// one that counts.
    /// </para>
    /// </summary>
    public static bool TryParse(string? path, out ArchivePath location)
    {
        location = default;
        string display = LongPath.Display(path ?? string.Empty);
        string extension = ArchiveService.ArchiveExtension;

        int from = 0;
        while (from < display.Length)
        {
            int found = display.IndexOf(extension, from, StringComparison.OrdinalIgnoreCase);
            if (found < 0)
                return false;

            int end = found + extension.Length;
            // ".zipped" is not a zip: the extension has to end the step.
            if ((end == display.Length || IsSeparator(display[end]))
                && File.Exists(LongPath.ForIo(display[..end])))
            {
                location = new ArchivePath(display[..end], Trim(display[end..]));
                return true;
            }
            from = found + 1;
        }
        return false;
    }

    /// <summary>An entry name as a zip writes it: forward slashes, bare at both ends.</summary>
    public static string Trim(string entryName) =>
        entryName.Replace('\\', '/').Trim('/');

    /// <summary>
    /// True when an entry name would climb out of the archive it is in.
    /// Nothing here writes files, so such a name cannot do damage — but it
    /// can put a row in a folder it does not belong to, and a listing that
    /// lies about where something is is worth less than one missing a row.
    /// </summary>
    public static bool IsSafeEntryName(string entryName)
    {
        if (entryName.Length == 0 || Path.IsPathRooted(entryName.Replace('/', Path.DirectorySeparatorChar)))
            return false;
        foreach (string step in Trim(entryName).Split('/'))
        {
            if (step is "." or "..")
                return false;
        }
        return true;
    }

    private static bool IsSeparator(char c) => Array.IndexOf(_separators, c) >= 0;
}
