namespace Rove.Core.Services;

public static class LongPath
{
    public const int WindowsMaxPath = 259;

    private const string Prefix = @"\\?\";
    private const string UncPrefix = @"\\?\UNC\";
    private const string DevicePrefix = @"\\.\";

    public static bool IsExtended(string path) =>
        path.StartsWith(Prefix, StringComparison.Ordinal)
        || path.StartsWith(DevicePrefix, StringComparison.Ordinal);

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

        return full.StartsWith(@"\\", StringComparison.Ordinal)
            ? UncPrefix + full[2..]
            : Prefix + full;
    }

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
