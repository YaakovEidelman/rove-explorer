using System.Diagnostics;
using System.Runtime.Versioning;

namespace Rove.Core.Services;

[SupportedOSPlatform("linux")]
public sealed class LinuxIconTheme
{
    private static readonly string[] _extensions = [".png", ".svg"];
    private const string FallbackTheme = "hicolor";
    private const int MaxInheritedThemes = 16;

    private readonly IReadOnlyList<string> _baseDirs;
    private readonly IReadOnlyList<string> _unthemedDirs;
    private readonly IReadOnlyList<ThemeIndex> _chain;

    private LinuxIconTheme(
        IReadOnlyList<string> baseDirs,
        IReadOnlyList<string> unthemedDirs,
        IReadOnlyList<ThemeIndex> chain
    )
    {
        _baseDirs = baseDirs;
        _unthemedDirs = unthemedDirs;
        _chain = chain;
    }

    public static LinuxIconTheme Load()
    {
        List<string> baseDirs = [];
        void Add(string dir)
        {
            if (Directory.Exists(dir) && !baseDirs.Contains(dir, StringComparer.Ordinal))
                baseDirs.Add(dir);
        }

        Add(Path.Combine(XdgPaths.Home, ".icons"));
        foreach (string dataDir in XdgPaths.DataDirs())
            Add(Path.Combine(dataDir, "icons"));

        List<string> unthemed = [];
        foreach (string dataDir in XdgPaths.DataDirs())
        {
            string pixmaps = Path.Combine(dataDir, "pixmaps");
            if (Directory.Exists(pixmaps) && !unthemed.Contains(pixmaps, StringComparer.Ordinal))
                unthemed.Add(pixmaps);
        }

        return new LinuxIconTheme(baseDirs, unthemed, BuildChain(baseDirs, DetectThemeName()));
    }

    public string? FindIconFile(string name, int size)
    {
        foreach (ThemeIndex theme in _chain)
        {
            if (FindInTheme(theme, name, size, exactOnly: true) is { } exact)
                return exact;
        }

        foreach (ThemeIndex theme in _chain)
        {
            if (FindInTheme(theme, name, size, exactOnly: false) is { } close)
                return close;
        }

        foreach (string dir in _unthemedDirs)
        {
            if (FirstExisting(dir, name) is { } fallback)
                return fallback;
        }

        return null;
    }

    private string? FindInTheme(ThemeIndex theme, string name, int size, bool exactOnly)
    {
        IEnumerable<ThemeDirectory> candidates = exactOnly
            ? theme.Directories.Where(d => d.Matches(size))
            : theme.Directories.OrderBy(d => d.DistanceTo(size));

        foreach (ThemeDirectory dir in candidates)
        {
            foreach (string baseDir in _baseDirs)
            {
                string folder = Path.Combine(baseDir, theme.Name, dir.Path);
                if (FirstExisting(folder, name) is { } file)
                    return file;
            }
        }
        return null;
    }

    private static string? FirstExisting(string folder, string name)
    {
        foreach (string extension in _extensions)
        {
            string file = Path.Combine(folder, name + extension);
            if (File.Exists(file))
                return file;
        }
        return null;
    }

    private static IReadOnlyList<ThemeIndex> BuildChain(IReadOnlyList<string> baseDirs, string? preferred)
    {
        List<ThemeIndex> chain = [];
        HashSet<string> seen = new(StringComparer.Ordinal);
        Queue<string> pending = new();

        if (!string.IsNullOrEmpty(preferred))
            pending.Enqueue(preferred);
        pending.Enqueue("Adwaita");
        pending.Enqueue(FallbackTheme);

        while (pending.Count > 0 && chain.Count < MaxInheritedThemes)
        {
            string name = pending.Dequeue();
            if (!seen.Add(name))
                continue;

            ThemeIndex? theme = ReadIndex(baseDirs, name);
            if (theme is null)
                continue;

            chain.Add(theme);
            foreach (string parent in theme.Inherits)
                pending.Enqueue(parent);
        }

        return chain;
    }

    private static ThemeIndex? ReadIndex(IReadOnlyList<string> baseDirs, string name)
    {
        foreach (string baseDir in baseDirs)
        {
            string index = Path.Combine(baseDir, name, "index.theme");
            if (!File.Exists(index))
                continue;

            try
            {
                return ParseIndex(name, File.ReadAllLines(index));
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }
        return null;
    }

    private static ThemeIndex ParseIndex(string name, string[] lines)
    {
        Dictionary<string, Dictionary<string, string>> sections = new(StringComparer.Ordinal);
        Dictionary<string, string>? current = null;

        foreach (string raw in lines)
        {
            string line = raw.Trim();
            if (line.Length == 0 || line[0] == '#')
                continue;

            if (line[0] == '[' && line[^1] == ']')
            {
                current = new Dictionary<string, string>(StringComparer.Ordinal);
                sections[line[1..^1]] = current;
                continue;
            }

            int eq = line.IndexOf('=');
            if (eq <= 0 || current is null)
                continue;
            current[line[..eq].Trim()] = line[(eq + 1)..].Trim();
        }

        sections.TryGetValue("Icon Theme", out Dictionary<string, string>? header);
        header ??= [];

        string[] inherits = Split(header, "Inherits");
        List<ThemeDirectory> directories = [];
        foreach (string path in Split(header, "Directories").Concat(Split(header, "ScaledDirectories")))
        {
            if (!sections.TryGetValue(path, out Dictionary<string, string>? section))
                continue;
            if (Number(section, "Scale", 1) != 1)
                continue;

            int size = Number(section, "Size", 0);
            if (size <= 0)
                continue;

            directories.Add(
                new ThemeDirectory(
                    path,
                    size,
                    section.GetValueOrDefault("Type", "Threshold"),
                    Number(section, "MinSize", size),
                    Number(section, "MaxSize", size),
                    Number(section, "Threshold", 2)
                )
            );
        }

        return new ThemeIndex(name, inherits, directories);
    }

    private static string[] Split(Dictionary<string, string> section, string key) =>
        section.TryGetValue(key, out string? value)
            ? value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : [];

    private static int Number(Dictionary<string, string> section, string key, int fallback) =>
        section.TryGetValue(key, out string? value) && int.TryParse(value, out int parsed)
            ? parsed
            : fallback;

    private static string? DetectThemeName()
    {
        if (Environment.GetEnvironmentVariable("ROVE_ICON_THEME") is { Length: > 0 } forced)
            return forced;

        foreach (string version in new[] { "gtk-4.0", "gtk-3.0" })
        {
            string ini = Path.Combine(XdgPaths.ConfigHome, version, "settings.ini");
            if (ReadIniValue(ini, "gtk-icon-theme-name") is { } gtk)
                return gtk;
        }

        string kde = Path.Combine(XdgPaths.ConfigHome, "kdeglobals");
        if (ReadIniValue(kde, "Theme") is { } kdeTheme)
            return kdeTheme;

        return ReadGSettingsIconTheme();
    }

    private static string? ReadGSettingsIconTheme()
    {
        try
        {
            ProcessStartInfo info = new("gsettings", "get org.gnome.desktop.interface icon-theme")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            using Process? process = Process.Start(info);
            if (process is null)
                return null;

            string output = process.StandardOutput.ReadToEnd();
            process.StandardError.ReadToEnd();
            if (!process.WaitForExit(1000))
            {
                process.Kill();
                return null;
            }
            if (process.ExitCode != 0)
                return null;

            string value = output.Trim().Trim('\'', '"');
            return value.Length > 0 ? value : null;
        }
        catch (Exception ex)
            when (ex is IOException or System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return null;
        }
    }

    private static string? ReadIniValue(string path, string key)
    {
        if (!File.Exists(path))
            return null;

        try
        {
            foreach (string raw in File.ReadLines(path))
            {
                string line = raw.Trim();
                int eq = line.IndexOf('=');
                if (eq <= 0)
                    continue;
                if (!line[..eq].Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
                    continue;

                string value = line[(eq + 1)..].Trim().Trim('"');
                return value.Length > 0 ? value : null;
            }
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }

        return null;
    }

    private sealed record ThemeIndex(
        string Name,
        IReadOnlyList<string> Inherits,
        IReadOnlyList<ThemeDirectory> Directories
    );

    private sealed record ThemeDirectory(
        string Path,
        int Size,
        string Type,
        int MinSize,
        int MaxSize,
        int Threshold
    )
    {
        public bool Matches(int size) =>
            Type switch
            {
                "Fixed" => Size == size,
                "Scalable" or "Scaled" => MinSize <= size && size <= MaxSize,
                _ => Size - Threshold <= size && size <= Size + Threshold,
            };

        public int DistanceTo(int size) =>
            Type switch
            {
                "Scalable" or "Scaled" when size < MinSize => MinSize - size,
                "Scalable" or "Scaled" when size > MaxSize => size - MaxSize,
                "Scalable" or "Scaled" => 0,
                _ => Math.Abs(Size - size),
            };
    }
}
