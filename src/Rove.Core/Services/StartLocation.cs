namespace Rove.Core.Services;

/// <summary>
/// Where a launch is asking Rove to open.
///
/// <para>
/// The desktop entry claims folders — it lists <c>inode/directory</c> among
/// the types it handles and takes a path on the command line — so "open this
/// folder with Rove" hands one over and expects to arrive there. A file is
/// worth accepting too: nobody who drops one on Rove means "show me my home
/// folder", they mean the folder it is sitting in.
/// </para>
/// </summary>
public static class StartLocation
{
    /// <summary>
    /// The folder <paramref name="args"/> asks for, or the usual starting
    /// place when they ask for nothing that exists. <paramref name="isDirectory"/>
    /// and <paramref name="isFile"/> stand in for the filesystem so the
    /// choosing can be tested without one.
    /// </summary>
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

    /// <summary>
    /// An argument as a full path. Launchers that were given <c>%U</c> rather
    /// than <c>%F</c> pass a <c>file://</c> URI, which is the same place said
    /// differently; anything else is taken as a path and made absolute.
    /// </summary>
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
