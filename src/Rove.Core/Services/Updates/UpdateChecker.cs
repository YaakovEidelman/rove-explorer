using System.Text.Json;

namespace Rove.Core.Services;

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

    public static bool IsOfferable(GitHubRelease release, Version running, string? skippedVersion) =>
        release.Version is { } version
        && version > running
        && !string.Equals(release.TagName, skippedVersion, StringComparison.OrdinalIgnoreCase);
}
