using Rove.UI.Services;
using Xunit;

namespace Rove.UI.Tests;

public class InstallDecisionTests
{
    private static readonly DateTime _built = new(2026, 9, 9, 12, 0, 0, DateTimeKind.Utc);

    private static BuildIdentity Build(string version, long size = 100, int minutesLater = 0) =>
        new(Version.Parse(version), size, _built.AddMinutes(minutesLater));

    private static InstallRecord Record(BuildIdentity identity) =>
        InstallRecord.For("/home/me/.local/lib/rove/Rove", identity,
            ["/home/me/.local/share/applications/rove.desktop"]);

    [Fact]
    public void WithNothingInstalledTheRunningBuildInstallsItself()
    {
        InstallStep step = DesktopInstall.Decide(Build("1.0.0.0"), null, binaryPresent: false, supportPresent: false);

        Assert.Equal(InstallStep.Install, step);
    }

    [Fact]
    public void TheInstalledBuildRunningAgainDoesNothing()
    {
        BuildIdentity same = Build("1.0.0.0");

        InstallStep step = DesktopInstall.Decide(same, Record(same), binaryPresent: true, supportPresent: true);

        Assert.Equal(InstallStep.Nothing, step);
    }

    [Fact]
    public void ANewerBuildTakesOver()
    {
        InstallStep step = DesktopInstall.Decide(
            Build("1.1.0.0"), Record(Build("1.0.0.0")), binaryPresent: true, supportPresent: true);

        Assert.Equal(InstallStep.Update, step);
    }

    [Fact]
    public void ARebuildOfTheSameVersionCountsAsNewer()
    {
        InstallStep step = DesktopInstall.Decide(
            Build("1.0.0.0", size: 120, minutesLater: 30), Record(Build("1.0.0.0")),
            binaryPresent: true, supportPresent: true);

        Assert.Equal(InstallStep.Update, step);
    }

    [Fact]
    public void AnOlderBuildLeavesTheInstallAlone()
    {
        InstallStep step = DesktopInstall.Decide(
            Build("0.9.0.0"), Record(Build("1.0.0.0")), binaryPresent: true, supportPresent: true);

        Assert.Equal(InstallStep.KeepInstalled, step);
    }

    [Fact]
    public void AMissingShortcutOrEntryIsRepairedWithoutTouchingTheBuild()
    {
        BuildIdentity same = Build("1.0.0.0");

        InstallStep step = DesktopInstall.Decide(same, Record(same), binaryPresent: true, supportPresent: false);

        Assert.Equal(InstallStep.Repair, step);
    }

    [Fact]
    public void AnInstallWhoseProgramWentMissingIsPutBack()
    {
        BuildIdentity same = Build("1.0.0.0");

        InstallStep step = DesktopInstall.Decide(same, Record(same), binaryPresent: false, supportPresent: true);

        Assert.Equal(InstallStep.Install, step);
    }

    [Fact]
    public void ACopyOfTheSameBuildElsewhereIsStillTheSameBuild()
    {
        Assert.True(Build("1.0.0.0").IsSameBuildAs(Build("1.0.0.0")));
        Assert.False(Build("1.0.0.0").IsSameBuildAs(Build("1.0.0.0", size: 101)));
        Assert.False(Build("1.0.0.0").IsSameBuildAs(Build("1.0.1.0")));
    }

    [Fact]
    public void ASecondsWorthOfClockDriftIsNotANewBuild()
    {
        BuildIdentity original = Build("1.0.0.0");
        BuildIdentity copied = original with { ModifiedUtc = original.ModifiedUtc.AddSeconds(1) };

        Assert.True(copied.IsSameBuildAs(original));
        Assert.False(copied.IsNewerThan(original));
    }
}
