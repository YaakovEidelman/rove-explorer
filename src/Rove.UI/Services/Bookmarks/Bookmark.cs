using System.Text.Json.Serialization;

namespace Rove.UI.Services;

public sealed record Bookmark(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("isDirectory")] bool IsDirectory
);
