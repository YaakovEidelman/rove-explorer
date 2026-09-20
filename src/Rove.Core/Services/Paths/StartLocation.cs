namespace Rove.Core.Services;

public static class StartLocation
{
    public static string From(string[]? args, Func<string, bool> isDirectory, Func<string, bool> isFile) =>
        ExplicitTarget(args, isDirectory, isFile) ?? PathCompare.DefaultStartDirectory();

    public static string? ExplicitTarget(string[]? args, Func<string, bool> isDirectory, Func<string, bool> isFile)
    {
        foreach (string arg in args ?? [])
        {
            if (arg.Length == 0 || arg[0] == '-')
                continue;

            if (Resolve(arg) is not { } path)
                continue;

            if (isDirectory(path))
                return path;

            if (isFile(path) && Path.GetDirectoryName(path) is { Length: > 0 } parent)
                return parent;
        }
        return null;
    }

    private static string? Resolve(string arg)
    {
        try
        {
            if (Uri.TryCreate(arg, UriKind.Absolute, out Uri? uri) && uri.Scheme == Uri.UriSchemeFile)
                return Path.GetFullPath(uri.LocalPath);
            return Path.GetFullPath(arg);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }
}
