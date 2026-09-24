using System.Text.Json.Serialization;

namespace Rove.Core.Services;

[JsonSerializable(typeof(GitHubRelease))]
internal partial class GitHubReleaseJson : JsonSerializerContext
{
}
