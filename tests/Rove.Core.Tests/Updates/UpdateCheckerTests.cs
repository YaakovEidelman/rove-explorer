using Rove.Core.Services;
using Xunit;

namespace Rove.Core.Tests;

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
