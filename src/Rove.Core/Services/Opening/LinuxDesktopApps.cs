using System.Runtime.Versioning;
using Rove.Core.Protocol;

namespace Rove.Core.Services;

[SupportedOSPlatform("linux")]
public static class LinuxDesktopApps
{
    private static readonly string[] _fileCodes = ["%f", "%F", "%u", "%U"];

    public static AppEntry[] Installed() =>
        Collect(XdgPaths.DataDirs().Select(dir => Path.Combine(dir, "applications")));

    public static AppEntry[] ForFile(string path)
    {
        if (GioMime.ContentType(path) is not { } type)
            return [];

        Dictionary<string, AppEntry> installed = Installed().ToDictionary(app => app.Id, StringComparer.Ordinal);
        return [.. GioMime.AppIds(type).Select(id => installed.GetValueOrDefault(id)).OfType<AppEntry>()];
    }

    internal static AppEntry[] Collect(IEnumerable<string> applicationDirs)
    {
        HashSet<string> seen = new(StringComparer.Ordinal);
        List<AppEntry> found = [];

        foreach (string dir in applicationDirs.Distinct(StringComparer.Ordinal).Where(Directory.Exists))
        {
            foreach (string file in Directory.EnumerateFiles(dir, "*.desktop", SearchOption.AllDirectories))
            {
                string id = Path.GetRelativePath(dir, file).Replace(Path.DirectorySeparatorChar, '-');
                if (seen.Add(id) && Read(file, id) is { } app)
                    found.Add(app);
            }
        }

        return [.. found.OrderBy(app => app.Name, StringComparer.OrdinalIgnoreCase)];
    }

    private static AppEntry? Read(string file, string id)
    {
        Dictionary<string, string> keys = ReadDesktopEntry(file);
        if (!keys.TryGetValue("Type", out string? type) || type != "Application")
            return null;
        if (IsTrue(keys, "NoDisplay") || IsTrue(keys, "Hidden"))
            return null;
        if (!keys.TryGetValue("Name", out string? name) || name.Length == 0)
            return null;
        if (!keys.TryGetValue("Exec", out string? exec) || !_fileCodes.Any(exec.Contains))
            return null;

        return new AppEntry(id, name, file);
    }

    private static bool IsTrue(Dictionary<string, string> keys, string key) =>
        keys.TryGetValue(key, out string? value) && value == "true";

    private static Dictionary<string, string> ReadDesktopEntry(string file)
    {
        Dictionary<string, string> keys = new(StringComparer.Ordinal);
        bool inEntry = false;
        try
        {
            foreach (string raw in File.ReadLines(file))
            {
                string line = raw.Trim();
                if (line.StartsWith('['))
                {
                    if (inEntry)
                        break;
                    inEntry = line == "[Desktop Entry]";
                    continue;
                }

                int equals = line.IndexOf('=');
                if (inEntry && equals > 0 && line[0] != '#')
                    keys.TryAdd(line[..equals].TrimEnd(), line[(equals + 1)..].TrimStart());
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
        return keys;
    }
}
