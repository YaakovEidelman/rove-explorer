using System.Text.Json;

namespace Rove.Core.Services;

public static class CustomTheme
{
    public static readonly ThemeColors Default = new(
        Mode: "dark",
        AppBackground: "#17191D",
        Surface: "#1F2228",
        SurfaceAlt: "#14161A",
        AppBorder: "#2E323A",
        TextPrimary: "#E6E8EB",
        TextSecondary: "#9AA1AC",
        Accent: "#6FA3F5",
        AccentSubtle: "#263650",
        Error: "#E5715C",
        MarkBar: "#D9A62E");

    public static void Save(ThemeColors colors)
    {
        string path = RovePaths.CustomThemeFile;
        try
        {
            if (Path.GetDirectoryName(path) is { Length: > 0 } parent)
                Directory.CreateDirectory(parent);
            File.WriteAllText(path, JsonSerializer.Serialize(colors, ThemeColorsJson.Default.ThemeColors));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
        }
    }

    public static ThemeColors? Load()
    {
        string path = RovePaths.CustomThemeFile;
        try
        {
            if (!File.Exists(path))
                return null;
            ThemeColors? colors = JsonSerializer.Deserialize(File.ReadAllText(path), ThemeColorsJson.Default.ThemeColors);
            return IsComplete(colors) ? colors : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            return null;
        }
    }

    private static bool IsComplete(ThemeColors? colors) =>
        colors is not null
        && !string.IsNullOrEmpty(colors.Mode)
        && !string.IsNullOrEmpty(colors.AppBackground)
        && !string.IsNullOrEmpty(colors.Surface)
        && !string.IsNullOrEmpty(colors.SurfaceAlt)
        && !string.IsNullOrEmpty(colors.AppBorder)
        && !string.IsNullOrEmpty(colors.TextPrimary)
        && !string.IsNullOrEmpty(colors.TextSecondary)
        && !string.IsNullOrEmpty(colors.Accent)
        && !string.IsNullOrEmpty(colors.AccentSubtle)
        && !string.IsNullOrEmpty(colors.Error)
        && !string.IsNullOrEmpty(colors.MarkBar);
}
