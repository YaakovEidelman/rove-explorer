using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rove.Core.Services;

public sealed record UpdateCheckState(
    [property: JsonPropertyName("lastCheckedUtc")] DateTime LastCheckedUtc,
    [property: JsonPropertyName("skippedVersion")] string? SkippedVersion = null
)
{
    public static UpdateCheckState? Read(string path)
    {
        try
        {
            return File.Exists(path)
                ? JsonSerializer.Deserialize(File.ReadAllText(path), UpdateCheckJson.Default.UpdateCheckState)
                : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            return null;
        }
    }

    public bool Write(string path)
    {
        try
        {
            if (Path.GetDirectoryName(path) is { Length: > 0 } parent)
                Directory.CreateDirectory(parent);
            File.WriteAllText(path, JsonSerializer.Serialize(this, UpdateCheckJson.Default.UpdateCheckState));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
