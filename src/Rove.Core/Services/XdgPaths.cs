using System.Runtime.Versioning;

namespace Rove.Core.Services;

[SupportedOSPlatform("linux")]
public static class XdgPaths
{
    public static string Home =>
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public static string DataHome =>
        Environment.GetEnvironmentVariable("XDG_DATA_HOME") is { Length: > 0 } dir
            ? dir
            : Path.Combine(Home, ".local", "share");

    public static string ConfigHome =>
        Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") is { Length: > 0 } dir
            ? dir
            : Path.Combine(Home, ".config");

    public static string StateHome =>
        Environment.GetEnvironmentVariable("XDG_STATE_HOME") is { Length: > 0 } dir
            ? dir
            : Path.Combine(Home, ".local", "state");

    public static IEnumerable<string> DataDirs()
    {
        yield return DataHome;

        string dirs = Environment.GetEnvironmentVariable("XDG_DATA_DIRS") is { Length: > 0 } d
            ? d
            : "/usr/local/share:/usr/share";

        foreach (string dir in dirs.Split(':', StringSplitOptions.RemoveEmptyEntries))
            yield return dir;
    }

    public static IEnumerable<string> ExistingDataDirs() =>
        DataDirs().Distinct(StringComparer.Ordinal).Where(Directory.Exists);
}
