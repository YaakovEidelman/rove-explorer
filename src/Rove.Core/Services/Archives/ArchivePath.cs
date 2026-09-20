namespace Rove.Core.Services;

public readonly record struct ArchivePath(string Archive, string Entry)
{
    private static readonly char[] _separators =
        [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];

    public bool IsRoot => Entry.Length == 0;

    public string FullPath => PathOf(Entry);

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

    public string PathOf(string entryName)
    {
        string archive = LongPath.Display(Archive);
        string trimmed = Trim(entryName);
        return trimmed.Length == 0
            ? archive
            : archive + Path.DirectorySeparatorChar + trimmed.Replace('/', Path.DirectorySeparatorChar);
    }

    public ArchivePath Down(string childName) =>
        new(Archive, Entry.Length == 0 ? Trim(childName) : Entry + '/' + Trim(childName));

    public static bool IsInside(string? path) => TryParse(path, out _);

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

    public static string Trim(string entryName) =>
        entryName.Replace('\\', '/').Trim('/');

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
