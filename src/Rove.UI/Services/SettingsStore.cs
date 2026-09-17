using Rove.Core.Services;
using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rove.UI.Services;

/// <summary>
/// Rove's own preferences. Small and few on purpose — anything that can live
/// as a keybinding or a command stays one; this is only for the handful of
/// things that are a choice of default rather than a verb.
/// </summary>
public sealed record AppSettings(
    [property: JsonPropertyName("theme")] string Theme = "System",
    [property: JsonPropertyName("showHiddenByDefault")] bool ShowHiddenByDefault = false,
    [property: JsonPropertyName("defaultView")] string DefaultView = "List",
    [property: JsonPropertyName("sortDownloadsByTime")] bool SortDownloadsByTime = true,
    [property: JsonPropertyName("autoUpdate")] bool AutoUpdate = false
);

/// <summary>
/// Reads and writes <see cref="AppSettings"/> to a small JSON file next to
/// the keybindings and bookmarks. Missing or unreadable reads as every
/// default rather than as an error — a broken settings file should not keep
/// the app from starting.
/// </summary>
public sealed class SettingsStore
{
    private readonly string _path;
    private AppSettings _current;

    /// <summary>Raised whenever a setting changes, so anything showing one can follow.</summary>
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
            // A setting that could not be written down still applies for
            // this run; losing it at the next launch beats interrupting the
            // user over a preferences file.
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

/// <summary>
/// The file's shape, worked out at compile time: a natively compiled build
/// cannot reflect over a type to find its properties.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AppSettings))]
internal partial class SettingsJson : JsonSerializerContext
{
}
