using System.Runtime.Versioning;

namespace Rove.Core.Services;

[SupportedOSPlatform("linux")]
public static class OmarchyTheme
{
    public static string CurrentDirectory =>
        Path.Combine(XdgPaths.StateHome, "omarchy", "current");

    public static string ColorsFile =>
        Path.Combine(CurrentDirectory, "theme", "colors.toml");

    public static bool IsAvailable => File.Exists(ColorsFile);

    public static ThemeColors? Load()
    {
        Dictionary<string, string> values = ReadValues();
        if (values.Count == 0)
            return null;

        if (!values.TryGetValue("background", out string? background)
            || !values.TryGetValue("foreground", out string? foreground)
            || !values.TryGetValue("accent", out string? accent))
            return null;

        return new ThemeColors(
            Mode: values.GetValueOrDefault("mode", "dark"),
            AppBackground: background,
            Surface: values.GetValueOrDefault("lighter_background", background),
            SurfaceAlt: values.GetValueOrDefault("dark_background", background),
            AppBorder: values.GetValueOrDefault("muted", values.GetValueOrDefault("selection", foreground)),
            TextPrimary: foreground,
            TextSecondary: values.GetValueOrDefault("dark_foreground", foreground),
            Accent: accent,
            AccentSubtle: values.GetValueOrDefault("selection", accent),
            Error: values.GetValueOrDefault("red", accent),
            MarkBar: values.GetValueOrDefault("yellow", accent)
        );
    }

    private static Dictionary<string, string> ReadValues()
    {
        Dictionary<string, string> values = new(StringComparer.Ordinal);
        try
        {
            if (!File.Exists(ColorsFile))
                return values;

            foreach (string line in File.ReadAllLines(ColorsFile))
            {
                string trimmed = line.Trim();
                int eq = trimmed.IndexOf('=');
                if (trimmed.Length == 0 || trimmed[0] == '#' || eq < 0)
                    continue;

                string key = trimmed[..eq].Trim();
                string value = trimmed[(eq + 1)..].Trim().Trim('"');
                if (key.Length > 0 && value.Length > 0)
                    values[key] = value;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
        return values;
    }
}
