namespace Rove.Core.Services;

/// <summary>
/// Where a half-typed path splits: the folder whose contents could finish it,
/// and the start of the name being typed in that folder. An empty
/// <see cref="Directory"/> means the text could not be read as a path at all.
/// Whether the folder is really there is the caller's question — it finds out
/// by trying to read it.
/// </summary>
public readonly record struct PathFragment(string Directory, string Prefix);

/// <summary>
/// The text side of completing a path in the path bar: cutting typed text
/// into a folder and a partial name, putting a chosen name back into it, and
/// working out how far a set of names agrees. No filesystem here — what is
/// actually in the folder is the caller's question.
/// </summary>
public static class PathCompletion
{
    /// <summary>Splits typed text at its last separator.</summary>
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

    /// <summary>
    /// The same text with <paramref name="name"/> in place of the partial one.
    /// A folder gets a trailing separator so the next completion carries on
    /// inside it, written with whichever separator was already being used.
    /// </summary>
    public static string Join(string typed, string name, bool isDirectory)
    {
        string text = typed ?? string.Empty;
        int cut = LastSeparator(text);
        string head = cut < 0 ? string.Empty : text[..(cut + 1)];
        char separator = cut < 0 ? Path.DirectorySeparatorChar : text[cut];
        return isDirectory ? head + name + separator : head + name;
    }

    /// <summary>Does this name start with what has been typed so far?</summary>
    public static bool Matches(string name, string prefix) =>
        prefix.Length == 0 || name.StartsWith(prefix, PathCompare.Comparison);

    /// <summary>
    /// How far the names agree, which is how far a single Tab can safely
    /// carry the text. Spelled with the first name's capitals, since on a
    /// filesystem that ignores case the others may disagree about it.
    /// </summary>
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

    /// <summary>
    /// On Windows both slashes separate; elsewhere only the forward one does,
    /// because a backslash is a character a file is allowed to be named with.
    /// </summary>
    private static int LastSeparator(string text) =>
        text.LastIndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]);
}
