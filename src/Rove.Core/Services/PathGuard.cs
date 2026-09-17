namespace Rove.Core.Services;

/// <summary>
/// Validates user-supplied item names before they are combined with a
/// directory path. Blocks path traversal (`..\foo`), separators, reserved
/// device names and other names the filesystem would reject or misplace.
/// </summary>
public static class PathGuard
{
    private static readonly char[] _invalidChars = Path.GetInvalidFileNameChars();

    private static readonly HashSet<string> _reservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    /// <summary>Returns null when the name is safe, otherwise a stable reason code.</summary>
    public static string? Validate(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "empty_name";
        if (name == "." || name == "..")
            return "invalid_name";
        if (name.IndexOfAny(_invalidChars) >= 0)
            return "invalid_characters";
        // GetInvalidFileNameChars covers '\\', '/', ':' on Windows, but only '/'
        // elsewhere — check explicitly so behavior is identical cross-platform.
        if (name.Contains('/') || name.Contains('\\') || name.Contains(':'))
            return "invalid_characters";
        if (OperatingSystem.IsWindows())
        {
            if (name.EndsWith('.') || name.EndsWith(' '))
                return "invalid_name";
            string stem = name.Split('.')[0];
            if (_reservedNames.Contains(stem))
                return "reserved_name";
        }
        return null;
    }

    /// <summary>
    /// Combines directory + validated name and verifies the result stays
    /// inside the directory. Returns null and sets reason on failure.
    /// </summary>
    public static string? SafeCombine(string directory, string name, out string? reason)
    {
        reason = Validate(name);
        if (reason is not null)
            return null;

        string combined = Path.GetFullPath(Path.Combine(LongPath.Display(directory), name));
        string root = Path.GetFullPath(LongPath.Display(directory));
        if (!root.EndsWith(Path.DirectorySeparatorChar))
            root += Path.DirectorySeparatorChar;

        if (!combined.StartsWith(root, OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal))
        {
            reason = "path_escapes_directory";
            return null;
        }
        return combined;
    }

    /// <summary>True when <paramref name="candidate"/> is the same path as, or nested under, <paramref name="ancestor"/>.</summary>
    public static bool IsSameOrDescendant(string ancestor, string candidate)
    {
        StringComparison cmp = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        string a = Path.TrimEndingDirectorySeparator(Path.GetFullPath(LongPath.Display(ancestor)));
        string c = Path.TrimEndingDirectorySeparator(Path.GetFullPath(LongPath.Display(candidate)));

        if (string.Equals(a, c, cmp))
            return true;
        return c.StartsWith(a + Path.DirectorySeparatorChar, cmp)
            || c.StartsWith(a + Path.AltDirectorySeparatorChar, cmp);
    }
}
