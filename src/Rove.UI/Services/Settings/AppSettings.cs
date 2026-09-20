using System.Text.Json.Serialization;

namespace Rove.UI.Services;

public sealed record AppSettings(
    [property: JsonPropertyName("theme")] string Theme = "System",
    [property: JsonPropertyName("showHiddenByDefault")] bool ShowHiddenByDefault = false,
    [property: JsonPropertyName("defaultView")] string DefaultView = "List",
    [property: JsonPropertyName("sortDownloadsByTime")] bool SortDownloadsByTime = true,
    [property: JsonPropertyName("autoUpdate")] bool AutoUpdate = false
);
