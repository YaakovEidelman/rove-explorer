using System.Text.Json.Serialization;

namespace Rove.Core.Services;

public sealed record ThemeColors(
    [property: JsonPropertyName("mode")] string Mode,
    [property: JsonPropertyName("appBackground")] string AppBackground,
    [property: JsonPropertyName("surface")] string Surface,
    [property: JsonPropertyName("surfaceAlt")] string SurfaceAlt,
    [property: JsonPropertyName("appBorder")] string AppBorder,
    [property: JsonPropertyName("textPrimary")] string TextPrimary,
    [property: JsonPropertyName("textSecondary")] string TextSecondary,
    [property: JsonPropertyName("accent")] string Accent,
    [property: JsonPropertyName("accentSubtle")] string AccentSubtle,
    [property: JsonPropertyName("error")] string Error,
    [property: JsonPropertyName("markBar")] string MarkBar
);
