using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Rove.Core.Services;

namespace Rove.UI.Services;

public static class ThemePalette
{
    private static readonly string[] _brushKeys =
    [
        "AppBackgroundBrush", "SurfaceBrush", "SurfaceAltBrush", "AppBorderBrush",
        "TextPrimaryBrush", "TextSecondaryBrush", "AccentBrush", "AccentSubtleBrush",
        "AccentTextBrush", "ErrorBrush", "ErrorSubtleBrush", "MarkBarBrush",
        "MarkBgBrush", "OverlayDimBrush",
    ];

    private static Dictionary<string, IBrush>? _stockLight;
    private static Dictionary<string, IBrush>? _stockDark;

    public static void Apply(string themeSetting, ThemeColors? palette)
    {
        if (Application.Current is not { } app)
            return;

        EnsureSnapshots(app);

        if (themeSetting is "Custom" or "Omarchy" && palette is not null)
        {
            ThemeVariant variant = string.Equals(palette.Mode, "light", StringComparison.OrdinalIgnoreCase)
                ? ThemeVariant.Light
                : ThemeVariant.Dark;
            WritePalette(app, variant, palette);
            app.RequestedThemeVariant = variant;
            return;
        }

        Restore(app, ThemeVariant.Light, _stockLight!);
        Restore(app, ThemeVariant.Dark, _stockDark!);
        app.RequestedThemeVariant = themeSetting switch
        {
            "Light" => ThemeVariant.Light,
            "Dark" => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };
    }

    private static void EnsureSnapshots(Application app)
    {
        _stockLight ??= Snapshot(app, ThemeVariant.Light);
        _stockDark ??= Snapshot(app, ThemeVariant.Dark);
    }

    private static Dictionary<string, IBrush> Snapshot(Application app, ThemeVariant variant)
    {
        Dictionary<string, IBrush> snapshot = new(StringComparer.Ordinal);
        foreach (string key in _brushKeys)
        {
            if (app.TryGetResource(key, variant, out object? value) && value is IBrush brush)
                snapshot[key] = brush;
        }
        return snapshot;
    }

    private static void Restore(Application app, ThemeVariant variant, Dictionary<string, IBrush> stock)
    {
        if (GetDictionary(app, variant) is not { } dict)
            return;
        foreach ((string key, IBrush brush) in stock)
            dict[key] = brush;
    }

    private static void WritePalette(Application app, ThemeVariant variant, ThemeColors palette)
    {
        if (GetDictionary(app, variant) is not { } dict)
            return;

        Color background = Color.Parse(palette.AppBackground);
        Color surface = Color.Parse(palette.Surface);
        Color surfaceAlt = Color.Parse(palette.SurfaceAlt);
        Color border = Color.Parse(palette.AppBorder);
        Color textPrimary = Color.Parse(palette.TextPrimary);
        Color textSecondary = Color.Parse(palette.TextSecondary);
        Color accent = Color.Parse(palette.Accent);
        Color accentSubtle = Color.Parse(palette.AccentSubtle);
        Color error = Color.Parse(palette.Error);
        Color markBar = Color.Parse(palette.MarkBar);

        dict["AppBackgroundBrush"] = new SolidColorBrush(background);
        dict["SurfaceBrush"] = new SolidColorBrush(surface);
        dict["SurfaceAltBrush"] = new SolidColorBrush(surfaceAlt);
        dict["AppBorderBrush"] = new SolidColorBrush(border);
        dict["TextPrimaryBrush"] = new SolidColorBrush(textPrimary);
        dict["TextSecondaryBrush"] = new SolidColorBrush(textSecondary);
        dict["AccentBrush"] = new SolidColorBrush(accent);
        dict["AccentSubtleBrush"] = new SolidColorBrush(accentSubtle);
        dict["AccentTextBrush"] = new SolidColorBrush(ContrastColor(accent));
        dict["ErrorBrush"] = new SolidColorBrush(error);
        dict["ErrorSubtleBrush"] = new SolidColorBrush(WithAlpha(error, 0x2A));
        dict["MarkBarBrush"] = new SolidColorBrush(markBar);
        dict["MarkBgBrush"] = new SolidColorBrush(WithAlpha(markBar, 0x2A));
        dict["OverlayDimBrush"] = new SolidColorBrush(
            WithAlpha(Colors.Black, variant == ThemeVariant.Light ? (byte)0x33 : (byte)0x55));
    }

    private static IResourceDictionary? GetDictionary(Application app, ThemeVariant variant) =>
        app.Resources.ThemeDictionaries.TryGetValue(variant, out IThemeVariantProvider? provider)
            ? provider as IResourceDictionary
            : null;

    private static Color ContrastColor(Color c)
    {
        double luminance = ((0.299 * c.R) + (0.587 * c.G) + (0.114 * c.B)) / 255.0;
        return luminance > 0.6 ? Color.Parse("#14161A") : Colors.White;
    }

    private static Color WithAlpha(Color c, byte alpha) => new(alpha, c.R, c.G, c.B);
}
