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

public class UpdateCheckStateTests
{
    [Fact]
    public void WhatWasWrittenComesBack()
    {
        using TempDir dir = new();
        string path = dir.Sub("update-check.json");
        DateTime checkedAt = new(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc);

        Assert.True(new UpdateCheckState(checkedAt, "v1.2.3").Write(path));
        UpdateCheckState? read = UpdateCheckState.Read(path);

        Assert.NotNull(read);
        Assert.Equal(checkedAt, read.LastCheckedUtc);
        Assert.Equal("v1.2.3", read.SkippedVersion);
    }

    [Fact]
    public void AMissingOrRuinedStateReadsAsNeverChecked()
    {
        using TempDir dir = new();

        Assert.Null(UpdateCheckState.Read(dir.Sub("nothing.json")));

        string junk = dir.File("junk.json", "{ this is not json");
        Assert.Null(UpdateCheckState.Read(junk));
    }
}

public class UpdateCheckerTests
{
    [Fact]
    public void NeverHavingCheckedIsDue()
    {
        Assert.True(UpdateChecker.DueForCheck(null, DateTime.UtcNow));
    }

    [Fact]
    public void ACheckFromMinutesAgoIsNotDueYet()
    {
        UpdateCheckState recent = new(DateTime.UtcNow.AddHours(-1), null);

        Assert.False(UpdateChecker.DueForCheck(recent, DateTime.UtcNow));
    }

    [Fact]
    public void ACheckFromMoreThanADayAgoIsDueAgain()
    {
        UpdateCheckState old = new(DateTime.UtcNow.AddHours(-25), null);

        Assert.True(UpdateChecker.DueForCheck(old, DateTime.UtcNow));
    }

    private static GitHubRelease Release(string tag) => new(tag, Body: null, Assets: []);

    [Fact]
    public void ANewerReleaseIsOfferable()
    {
        Assert.True(UpdateChecker.IsOfferable(Release("v1.1.0"), new Version(1, 0, 0), skippedVersion: null));
    }

    [Fact]
    public void TheSameOrOlderReleaseIsNotOfferable()
    {
        Assert.False(UpdateChecker.IsOfferable(Release("v1.0.0"), new Version(1, 0, 0), skippedVersion: null));
        Assert.False(UpdateChecker.IsOfferable(Release("v0.9.0"), new Version(1, 0, 0), skippedVersion: null));
    }

    [Fact]
    public void AReleaseTheUserSkippedIsNotOfferedAgain()
    {
        Assert.False(UpdateChecker.IsOfferable(Release("v1.1.0"), new Version(1, 0, 0), skippedVersion: "v1.1.0"));
    }
}
