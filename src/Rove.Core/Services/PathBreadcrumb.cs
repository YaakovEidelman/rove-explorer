namespace Rove.Core.Services;

/// <summary>
/// One step of a path: what to show for it, and where that step lands. The
/// two differ — a step shows only its own name, but stands for everything
/// leading up to it.
/// </summary>
public readonly record struct PathCrumb(string Label, string FullPath, bool IsLast = false);

/// <summary>
/// Breaks a path into the steps it is made of, so the bar at the top can show
/// each one in a box of its own rather than as one long line of text. The
/// root counts as a step: it is where the path starts, and on Windows it is
/// also the only part that says which disk this is.
/// </summary>
public static class PathBreadcrumb
{
    private static readonly char[] _separators =
        [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];

    public static PathCrumb[] Of(string path)
    {
        string display = LongPath.Display(path ?? string.Empty).Trim();
        if (display.Length == 0)
            return [];

        string root = Root(display);
        List<PathCrumb> crumbs = [];
        if (root.Length > 0)
            crumbs.Add(new(root, root));

        string walked = root;
        foreach (string step in display[root.Length..].Split(_separators, StringSplitOptions.RemoveEmptyEntries))
        {
            walked = walked.Length == 0 ? step : Path.Combine(walked, step);
            crumbs.Add(new(step, walked));
        }

        int last = crumbs.Count - 1;
        crumbs[last] = crumbs[last] with { IsLast = true };
        return [.. crumbs];
    }

    /// <summary>
    /// Where the path starts — a drive, a network share, or the single slash
    /// everything hangs off. A path with no root at all is one measured from
    /// somewhere else, and has no first step to show.
    /// </summary>
    private static string Root(string display)
    {
        try
        {
            return Path.GetPathRoot(display) ?? string.Empty;
        }
        catch (ArgumentException)
        {
            return string.Empty;
        }
    }
}
