using Rove.Core.Services;
using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rove.UI.Services;

public sealed record TabSession(
    [property: JsonPropertyName("directory")] string Directory,
    [property: JsonPropertyName("title")] string? Title = null
);

public sealed record AppSession(
    [property: JsonPropertyName("tabs")] TabSession[] Tabs,
    [property: JsonPropertyName("activeIndex")] int ActiveIndex = 0
);

public sealed class SessionStore
{
    private readonly string _path;

    public SessionStore() : this(RovePaths.SessionFile)
    {
    }

    public SessionStore(string path)
    {
        _path = path;
    }

    public AppSession? Load()
    {
        try
        {
            if (!File.Exists(_path))
                return null;
            return JsonSerializer.Deserialize(File.ReadAllText(_path), SessionJson.Default.AppSession);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            return null;
        }
    }

    public void Save(AppSession session)
    {
        try
        {
            if (Path.GetDirectoryName(_path) is { Length: > 0 } parent)
                Directory.CreateDirectory(parent);
            File.WriteAllText(_path, JsonSerializer.Serialize(session, SessionJson.Default.AppSession));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
        }
    }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AppSession))]
internal partial class SessionJson : JsonSerializerContext
{
}
