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

    /// <summary>
    /// Breaks the path into steps, same as always, except that when it sits
    /// at or under <paramref name="collapse"/>'s root, the steps down to and
    /// including that root are folded into a single crumb carrying its
    /// label — so a place like the trash can show as "Trash" rather than the
    /// real path it happens to live at.
    /// </summary>
    public static PathCrumb[] Of(string path, (string Root, string Label)? collapse = null)
    {
        if (string.IsNullOrWhiteSpace(path) || collapse is not { } fold
            || !PathGuard.IsSameOrDescendant(fold.Root, path))
        {
            return Of(path ?? string.Empty);
        }

        string display = LongPath.Display(path).Trim();
        if (PathCompare.PathMatches(fold.Root, display))
            return [new(fold.Label, fold.Root, IsLast: true)];

        // IsSameOrDescendant canonicalizes before comparing, so a display
        // form that still doesn't literally start with the root (a trailing
        // separator mismatch, say) is left alone rather than sliced wrong.
        if (!display.StartsWith(fold.Root, PathCompare.Comparison))
            return Of(path);

        string remainder = display[fold.Root.Length..].TrimStart(_separators);
        List<PathCrumb> crumbs = [new(fold.Label, fold.Root), .. StepsFrom(fold.Root, remainder)];
        int last = crumbs.Count - 1;
        crumbs[last] = crumbs[last] with { IsLast = true };
        return [.. crumbs];
    }

    public static PathCrumb[] Of(string path)
    {
        string display = LongPath.Display(path ?? string.Empty).Trim();
        if (display.Length == 0)
            return [];

        string root = Root(display);
        List<PathCrumb> crumbs = [];
        if (root.Length > 0)
            crumbs.Add(new(root, root));

        crumbs.AddRange(StepsFrom(root, display[root.Length..]));

        int last = crumbs.Count - 1;
        crumbs[last] = crumbs[last] with { IsLast = true };
        return [.. crumbs];
    }

    private static IEnumerable<PathCrumb> StepsFrom(string walked, string remainder)
    {
        foreach (string step in remainder.Split(_separators, StringSplitOptions.RemoveEmptyEntries))
        {
            walked = walked.Length == 0 ? step : Path.Combine(walked, step);
            yield return new(step, walked);
        }
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
