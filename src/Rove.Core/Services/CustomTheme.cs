using System.Text.Json;

namespace Rove.Core.Services;

public static class CustomTheme
{
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
