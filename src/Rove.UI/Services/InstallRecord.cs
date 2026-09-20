using Rove.Core.Services;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rove.UI.Services;

public readonly record struct BuildIdentity(Version Version, long Size, DateTime ModifiedUtc)
{
    private static readonly TimeSpan _timeSlack = TimeSpan.FromSeconds(2);

    public static BuildIdentity? Of(string path, Version version)
    {
        try
        {
            FileInfo file = new(path);
            return file.Exists ? new BuildIdentity(version, file.Length, file.LastWriteTimeUtc) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    public bool IsSameBuildAs(BuildIdentity other) =>
        Version == other.Version
        && Size == other.Size
        && (ModifiedUtc - other.ModifiedUtc).Duration() <= _timeSlack;

    public bool IsNewerThan(BuildIdentity other) =>
        Version != other.Version
            ? Version > other.Version
            : ModifiedUtc > other.ModifiedUtc + _timeSlack;
}

public sealed record InstallRecord(
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("binary")] string Binary,
    [property: JsonPropertyName("size")] long Size,
    [property: JsonPropertyName("modifiedUtc")] DateTime ModifiedUtc,
    [property: JsonPropertyName("files")] string[] Files
)
{

    [JsonIgnore]
    public BuildIdentity Identity =>
        new(System.Version.TryParse(Version, out Version? parsed) ? parsed : new Version(0, 0), Size, ModifiedUtc);

    public static InstallRecord For(string binary, BuildIdentity identity, string[] files) =>
        new(identity.Version.ToString(), binary, identity.Size, identity.ModifiedUtc, files);

    public static InstallRecord? Read(string path)
    {
        try
        {
            if (!File.Exists(path))
                return null;
            InstallRecord? record = JsonSerializer.Deserialize(File.ReadAllText(path), InstallJson.Default.InstallRecord);
            return record is { Binary.Length: > 0 } ? record : null;
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
            File.WriteAllText(path, JsonSerializer.Serialize(this, InstallJson.Default.InstallRecord));
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
            if (Path.GetDirectoryName(path) is { Length: > 0 } parent
                && Directory.Exists(parent)
                && !Directory.EnumerateFileSystemEntries(parent).Any())
            {
                Directory.Delete(parent);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(InstallRecord))]
internal partial class InstallJson : JsonSerializerContext
{
}

public enum InstallStep
{
    Nothing,

    Install,

    Update,

    Repair,

    KeepInstalled,
}
