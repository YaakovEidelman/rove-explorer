using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rove.Core.Services;

public sealed record DefaultFileManagerState(
    [property: JsonPropertyName("priorDefault")] string? PriorDefault
)
{
    public static DefaultFileManagerState For(string? priorDefault) => new(priorDefault);

    public static DefaultFileManagerState? Read(string path)
    {
        try
        {
            return File.Exists(path)
                ? JsonSerializer.Deserialize(File.ReadAllText(path), DefaultFileManagerJson.Default.DefaultFileManagerState)
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
            File.WriteAllText(path, JsonSerializer.Serialize(this, DefaultFileManagerJson.Default.DefaultFileManagerState));
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
