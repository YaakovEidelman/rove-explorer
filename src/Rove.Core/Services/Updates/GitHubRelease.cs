using System.Text.Json.Serialization;

namespace Rove.Core.Services;

public sealed record GitHubRelease(
    [property: JsonPropertyName("tag_name")] string TagName,
    [property: JsonPropertyName("body")] string? Body,
    [property: JsonPropertyName("assets")] GitHubAsset[] Assets
)
{
    [JsonIgnore]
    public Version? Version =>
        System.Version.TryParse(TagName.TrimStart('v'), out Version? version) ? version : null;

    public GitHubAsset? AssetNamed(string name) =>
        Array.Find(Assets, asset => string.Equals(asset.Name, name, StringComparison.OrdinalIgnoreCase));
}
