using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rove.Core.Services;

public sealed record GitHubAsset(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("browser_download_url")] string BrowserDownloadUrl
);

/// <summary>What GitHub's "latest release" endpoint answers with — only the fields Rove reads.</summary>
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

[JsonSerializable(typeof(GitHubRelease))]
internal partial class GitHubReleaseJson : JsonSerializerContext
{
}

/// <summary>Rove's own note of when it last asked, and any release the user said to skip.</summary>
public sealed record UpdateCheckState(
    [property: JsonPropertyName("lastCheckedUtc")] DateTime LastCheckedUtc,
    [property: JsonPropertyName("skippedVersion")] string? SkippedVersion = null
)
{
    /// <summary>Anything unreadable reads as "never checked" — the check simply runs again.</summary>
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

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(UpdateCheckState))]
internal partial class UpdateCheckJson : JsonSerializerContext
{
}

/// <summary>
/// Asks GitHub, once a day at most, whether a newer stable release exists.
/// Anything that goes wrong — no network, a private repo answering 401, a
/// timeout — reads as "no update found" rather than as an error: a check
/// that cannot happen is not a reason to bother anyone.
/// </summary>
public static class UpdateChecker
{
    private const string UserAgent = "rove-explorer-update-checker";
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(24);

    public static bool DueForCheck(UpdateCheckState? state, DateTime nowUtc) =>
        state is null || nowUtc - state.LastCheckedUtc >= CheckInterval;

    public static async Task<GitHubRelease?> FetchLatestAsync(
        HttpClient client, string owner, string repo, CancellationToken ct)
    {
        try
        {
            using HttpRequestMessage request = new(HttpMethod.Get,
                $"https://api.github.com/repos/{owner}/{repo}/releases/latest");
            request.Headers.UserAgent.ParseAdd(UserAgent);
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            using HttpResponseMessage response = await client.SendAsync(request, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return null;
            using Stream stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            return await JsonSerializer.DeserializeAsync(stream, GitHubReleaseJson.Default.GitHubRelease, ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>A release is worth surfacing if it is newer than what's running and not the one skipped.</summary>
    public static bool IsOfferable(GitHubRelease release, Version running, string? skippedVersion) =>
        release.Version is { } version
        && version > running
        && !string.Equals(release.TagName, skippedVersion, StringComparison.OrdinalIgnoreCase);
}
