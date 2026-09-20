namespace Rove.Core.Services;

public readonly record struct PathFragment(string Directory, string Prefix);

public static class PathCompletion
{
    public static PathFragment Split(string typed, string currentDirectory)
    {
        string text = typed ?? string.Empty;
        int cut = LastSeparator(text);
        string prefix = text[(cut + 1)..];

        if (cut < 0)
            return new(currentDirectory, prefix);

        string head = text[..(cut + 1)];
        return new(PathResolver.Resolve(head, currentDirectory) ?? string.Empty, prefix);
    }

    public static string Join(string typed, string name, bool isDirectory)
    {
        string text = typed ?? string.Empty;
        int cut = LastSeparator(text);
        string head = cut < 0 ? string.Empty : text[..(cut + 1)];
        char separator = cut < 0 ? Path.DirectorySeparatorChar : text[cut];
        return isDirectory ? head + name + separator : head + name;
    }

    public static bool Matches(string name, string prefix) =>
        prefix.Length == 0 || name.StartsWith(prefix, PathCompare.Comparison);

    public static string LongestCommonPrefix(IReadOnlyList<string> names)
    {
        if (names.Count == 0)
            return string.Empty;

        string first = names[0];
        int shared = first.Length;
        foreach (string name in names)
        {
            int i = 0;
            int limit = Math.Min(shared, name.Length);
            while (i < limit && SameCharacter(first[i], name[i]))
                i++;
            shared = i;
        }
        return first[..shared];
    }

    private static bool SameCharacter(char a, char b) =>
        OperatingSystem.IsWindows()
            ? char.ToUpperInvariant(a) == char.ToUpperInvariant(b)
            : a == b;

    private static int LastSeparator(string text) =>
        text.LastIndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]);
}
