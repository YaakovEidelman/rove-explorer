using Rove.Core.Services;
using Xunit;

namespace Rove.Core.Tests;

public class GitHubReleaseTests
{
    private static GitHubRelease Release(string tag, params GitHubAsset[] assets) =>
        new(tag, Body: null, assets);

    [Fact]
    public void ALeadingVIsStrippedBeforeParsingTheVersion()
    {
        Assert.Equal(new Version(1, 2, 3), Release("v1.2.3").Version);
    }

    [Fact]
    public void ATagWithNoVersionInItParsesToNothing()
    {
        Assert.Null(Release("latest-build").Version);
    }

    [Fact]
    public void AnAssetIsFoundByNameWithoutCaseMattering()
    {
        GitHubRelease release = Release("v1.0.0", new GitHubAsset("Rove-Win-X64.zip", "https://example/asset"));

        Assert.NotNull(release.AssetNamed("rove-win-x64.zip"));
        Assert.Null(release.AssetNamed("rove-linux-x64.tar.gz"));
    }
}
