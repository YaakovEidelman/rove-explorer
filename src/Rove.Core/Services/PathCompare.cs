namespace Rove.Core.Services;

public static class PathCompare
{
    private static readonly StringComparison _comparisonType = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    private static readonly StringComparer _comparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    public static StringComparison Comparison => _comparisonType;

    /// <summary>Same rule as <see cref="PathMatches"/>, for sets and groupings.</summary>
    public static StringComparer Comparer => _comparer;

    public static bool PathMatches(string value, string compare)
    {
        return LongPath.Display(value).Equals(LongPath.Display(compare), _comparisonType);
    }

    /// <summary>Sensible starting directory: the user's home folder, falling back to the drive root.</summary>
    public static string DefaultStartDirectory()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(home) && Directory.Exists(home))
            return home;
        return OSRootPath();
    }

    public static string OSRootPath()
    {
        if (OperatingSystem.IsWindows())
        {
            string? root = Path.GetPathRoot(Environment.SystemDirectory);
            return string.IsNullOrWhiteSpace(root) ? @"C:\" : root;
        }
        return "/";
    }
}
