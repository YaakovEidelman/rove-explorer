namespace Rove.Core.Services;

public static class PathBreadcrumb
{
    private static readonly char[] _separators =
        [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];

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
