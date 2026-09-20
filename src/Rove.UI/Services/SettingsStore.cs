using Rove.Core.Services;
using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rove.UI.Services;

public sealed record AppSettings(
    [property: JsonPropertyName("theme")] string Theme = "System",
    [property: JsonPropertyName("showHiddenByDefault")] bool ShowHiddenByDefault = false,
    [property: JsonPropertyName("defaultView")] string DefaultView = "List",
    [property: JsonPropertyName("sortDownloadsByTime")] bool SortDownloadsByTime = true,
    [property: JsonPropertyName("autoUpdate")] bool AutoUpdate = false
);

public sealed class SettingsStore
{
    private readonly string _path;
    private AppSettings _current;

    public event Action? Changed;

    public SettingsStore() : this(RovePaths.SettingsFile)
    {
    }

    public SettingsStore(string path)
    {
        _path = path;
        _current = Read(path);
    }

    public AppSettings Current => _current;

    public void Update(AppSettings settings)
    {
        _current = settings;
        Changed?.Invoke();
        Save();
    }

    private void Save()
    {
        try
        {
            if (Path.GetDirectoryName(_path) is { Length: > 0 } parent)
                Directory.CreateDirectory(parent);
            File.WriteAllText(_path, JsonSerializer.Serialize(_current, SettingsJson.Default.AppSettings));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
        }
    }

    private static AppSettings Read(string path)
    {
        try
        {
            if (!File.Exists(path))
                return new();
            return JsonSerializer.Deserialize(File.ReadAllText(path), SettingsJson.Default.AppSettings) ?? new();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            return new();
        }
    }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AppSettings))]
internal partial class SettingsJson : JsonSerializerContext
{
}
