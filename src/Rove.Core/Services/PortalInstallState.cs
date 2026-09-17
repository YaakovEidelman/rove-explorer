using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rove.Core.Services;

public sealed record PortalInstallState(
    [property: JsonPropertyName("priorContent")] string? PriorContent
)
{
    public static PortalInstallState For(string? priorContent) => new(priorContent);

    public static PortalInstallState? Read(string path)
    {
        try
        {
            return File.Exists(path)
                ? JsonSerializer.Deserialize(File.ReadAllText(path), PortalInstallJson.Default.PortalInstallState)
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
            File.WriteAllText(path, JsonSerializer.Serialize(this, PortalInstallJson.Default.PortalInstallState));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static void Delete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(PortalInstallState))]
internal partial class PortalInstallJson : JsonSerializerContext
{
}
