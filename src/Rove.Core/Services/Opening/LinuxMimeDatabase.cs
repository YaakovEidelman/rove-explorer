using System.Runtime.Versioning;

namespace Rove.Core.Services;

[SupportedOSPlatform("linux")]
public sealed class LinuxMimeDatabase
{
    private readonly Dictionary<string, (string Mime, int Weight)> _byExtension = new(
        StringComparer.OrdinalIgnoreCase
    );
    private readonly Dictionary<string, string> _themeIcons = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _genericIcons = new(StringComparer.Ordinal);

    public static LinuxMimeDatabase Load()
    {
        LinuxMimeDatabase db = new();
        foreach (string dataDir in XdgPaths.ExistingDataDirs())
        {
            string mimeDir = Path.Combine(dataDir, "mime");
            if (!Directory.Exists(mimeDir))
                continue;

            db.ReadGlobs2(Path.Combine(mimeDir, "globs2"));
            db.ReadGlobs(Path.Combine(mimeDir, "globs"));
            db.ReadIconMap(Path.Combine(mimeDir, "icons"), db._themeIcons);
            db.ReadIconMap(Path.Combine(mimeDir, "generic-icons"), db._genericIcons);
        }
        return db;
    }

    public string? MimeForExtension(string extension)
    {
        if (string.IsNullOrEmpty(extension))
            return null;
        string ext = extension.StartsWith('.') ? extension[1..] : extension;
        return _byExtension.TryGetValue(ext, out (string Mime, int Weight) hit) ? hit.Mime : null;
    }

    public IEnumerable<string> ExtensionsForMimePattern(string mimePattern)
    {
        if (mimePattern.EndsWith("/*", StringComparison.Ordinal))
        {
            string prefix = mimePattern[..^1];
            return _byExtension
                .Where(kv => kv.Value.Mime.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Select(kv => kv.Key);
        }

        return _byExtension
            .Where(kv => string.Equals(kv.Value.Mime, mimePattern, StringComparison.OrdinalIgnoreCase))
            .Select(kv => kv.Key);
    }

    public string? ThemeIcon(string mime) =>
        _themeIcons.TryGetValue(mime, out string? icon) ? icon : null;

    public string? GenericIcon(string mime) =>
        _genericIcons.TryGetValue(mime, out string? icon) ? icon : null;

    private void ReadGlobs2(string path)
    {
        foreach (string line in ReadLines(path))
        {
            string[] parts = line.Split(':');
            if (parts.Length < 3 || !int.TryParse(parts[0], out int weight))
                continue;
            AddGlob(parts[2], parts[1], weight);
        }
    }

    private void ReadGlobs(string path)
    {
        foreach (string line in ReadLines(path))
        {
            string[] parts = line.Split(':');
            if (parts.Length < 2)
                continue;
            AddGlob(parts[1], parts[0], 50);
        }
    }

    private void ReadIconMap(string path, Dictionary<string, string> target)
    {
        foreach (string line in ReadLines(path))
        {
            string[] parts = line.Split(':');
            if (parts.Length < 2)
                continue;
            target.TryAdd(parts[0], parts[1]);
        }
    }

    private void AddGlob(string glob, string mime, int weight)
    {
        if (!glob.StartsWith("*.", StringComparison.Ordinal) || glob.Length < 3)
            return;
        string ext = glob[2..];
        if (ext.Contains('*') || ext.Contains('?') || ext.Contains('['))
            return;

        if (_byExtension.TryGetValue(ext, out (string Mime, int Weight) existing) && existing.Weight >= weight)
            return;
        _byExtension[ext] = (mime, weight);
    }

    private static IEnumerable<string> ReadLines(string path)
    {
        if (!File.Exists(path))
            yield break;

        IEnumerator<string> lines;
        try
        {
            lines = File.ReadLines(path).GetEnumerator();
        }
        catch (IOException)
        {
            yield break;
        }
        catch (UnauthorizedAccessException)
        {
            yield break;
        }

        using (lines)
        {
            while (true)
            {
                try
                {
                    if (!lines.MoveNext())
                        break;
                }
                catch (IOException)
                {
                    break;
                }

                string line = lines.Current;
                if (line.Length == 0 || line[0] == '#')
                    continue;
                yield return line;
            }
        }
    }
}
