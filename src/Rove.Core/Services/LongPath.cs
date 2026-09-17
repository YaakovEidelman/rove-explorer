namespace Rove.Core.Services;

/// <summary>
/// Windows rejects most paths longer than 259 characters unless they are
/// handed to the OS in "extended" form — <c>\\?\C:\...</c>, or
/// <c>\\?\UNC\server\share\...</c> for network shares. Everything that
/// actually touches the filesystem goes through <see cref="ForIo"/>; anything
/// shown to the user or compared against another path goes through
/// <see cref="Display"/> so the prefix never leaks out of the IO layer.
/// </summary>
public static class LongPath
{
    /// <summary>Longest path Windows accepts without the extended prefix.</summary>
    public const int WindowsMaxPath = 259;

    private const string Prefix = @"\\?\";
    private const string UncPrefix = @"\\?\UNC\";
    private const string DevicePrefix = @"\\.\";

    public static bool IsExtended(string path) =>
        path.StartsWith(Prefix, StringComparison.Ordinal)
        || path.StartsWith(DevicePrefix, StringComparison.Ordinal);

    /// <summary>
    /// The form to hand to File/Directory APIs. Short paths and non-Windows
    /// paths come back untouched, so nothing changes for the common case.
    /// </summary>
    public static string ForIo(string path)
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrEmpty(path) || IsExtended(path))
            return path;

        string full;
        try
        {
            full = Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return path;
        }

        if (full.Length <= WindowsMaxPath)
            return path;

        // The extended form turns normalization off, so it has to be applied
        // to the already-normalized path, never to the raw input.
        return full.StartsWith(@"\\", StringComparison.Ordinal)
            ? UncPrefix + full[2..]
            : Prefix + full;
    }

    /// <summary>The plain form: what the user sees and what paths are compared as.</summary>
    public static string Display(string path)
    {
        if (string.IsNullOrEmpty(path))
            return path;
        if (path.StartsWith(UncPrefix, StringComparison.Ordinal))
            return @"\\" + path[UncPrefix.Length..];
        if (path.StartsWith(Prefix, StringComparison.Ordinal))
            return path[Prefix.Length..];
        return path;
    }

    /// <summary>True when this path only works in extended form on this OS.</summary>
    public static bool NeedsExtendedForm(string path)
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrEmpty(path))
            return false;
        try
        {
            return Path.GetFullPath(Display(path)).Length > WindowsMaxPath;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }
}
