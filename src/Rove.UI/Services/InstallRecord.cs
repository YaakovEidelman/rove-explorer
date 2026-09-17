using Rove.Core.Services;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rove.UI.Services;

/// <summary>
/// Which build a file holds. Two files with the same version, the same size
/// and the same timestamp are the same build, wherever they sit — that is
/// what lets a launch answer "am I already installed?" without comparing a
/// hundred megabytes byte for byte.
/// </summary>
public readonly record struct BuildIdentity(Version Version, long Size, DateTime ModifiedUtc)
{
    /// <summary>Timestamps survive a copy, but not always to the tick: FAT keeps whole seconds.</summary>
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

    /// <summary>
    /// A higher version wins. Failing that — every build of a version in
    /// progress carries the same number — the one built later wins.
    /// </summary>
    public bool IsNewerThan(BuildIdentity other) =>
        Version != other.Version
            ? Version > other.Version
            : ModifiedUtc > other.ModifiedUtc + _timeSlack;
}

/// <summary>What the last install put where. Written after the fact, read at startup.</summary>
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

    /// <summary>Anything unreadable reads as "no record" — the install simply runs again.</summary>
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

    /// <summary>Drops the record, and the folder holding it when nothing else is in it.</summary>
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

/// <summary>
/// The record's shape, worked out at compile time: a natively compiled build
/// cannot reflect over a type to find its properties.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(InstallRecord))]
internal partial class InstallJson : JsonSerializerContext
{
}

/// <summary>What a launch has to do about the install, if anything.</summary>
public enum InstallStep
{
    /// <summary>This build is the installed one and everything it needs is there.</summary>
    Nothing,

    /// <summary>Nothing is installed, or what was is gone.</summary>
    Install,

    /// <summary>This build is newer than the installed one.</summary>
    Update,

    /// <summary>The right build is installed, but something around it went missing.</summary>
    Repair,

    /// <summary>
    /// Something newer is already installed. Running an older copy is not a
    /// reason to put it back — the newest build stays the installed one.
    /// </summary>
    KeepInstalled,
}
