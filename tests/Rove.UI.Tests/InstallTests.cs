using Rove.UI.Services;
using System.Text;
using Xunit;

namespace Rove.UI.Tests;

public sealed class InstallHome : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "rove-install-" + Guid.NewGuid().ToString("N"));

    public string DataHome => Path.Combine(Root, "share");

    public string BinDir => Path.Combine(Root, "bin");

    public string LibDir => Path.Combine(Root, "lib");

    public string StartMenu => Path.Combine(Root, "start-menu");

    public string Sub(params string[] parts) => Path.Combine([Root, .. parts]);

    public string Build(string folder, string content = "a binary", string executable = "Rove")
    {
        string dir = Path.Combine(Root, folder);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, executable), content);
        File.WriteAllText(Path.Combine(dir, "libSkiaSharp.so"), "skia " + content);
        File.WriteAllText(Path.Combine(dir, "libHarfBuzzSharp.so"), "harfbuzz");
        File.WriteAllText(Path.Combine(dir, "Rove.pdb"), "symbols nobody needs");
        return Path.Combine(dir, executable);
    }

    public static byte[]? Asset(string name) => Encoding.UTF8.GetBytes("asset:" + name);

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch
        {
        }
    }
}

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

public class InstallRecordTests
{
    [Fact]
    public void WhatWasWrittenComesBack()
    {
        using InstallHome home = new();
        string path = home.Sub("installed.json");
        BuildIdentity identity = new(new Version(1, 2, 3, 4), 4096, new DateTime(2026, 9, 9, 12, 0, 0, DateTimeKind.Utc));

        Assert.True(InstallRecord.For("/opt/rove/Rove", identity, ["/a", "/b"]).Write(path));
        InstallRecord? read = InstallRecord.Read(path);

        Assert.NotNull(read);
        Assert.Equal("/opt/rove/Rove", read.Binary);
        Assert.Equal(identity, read.Identity);
        Assert.Equal(["/a", "/b"], read.Files);
    }

    [Fact]
    public void DeletingTheRecordTakesItsFolderWithIt()
    {
        using InstallHome home = new();
        string path = Path.Combine(home.Root, "state", "installed.json");
        BuildIdentity identity = new(new Version(1, 0), 1, DateTime.UnixEpoch);
        InstallRecord.For("/opt/rove/Rove", identity, []).Write(path);

        InstallRecord.Delete(path);

        Assert.False(File.Exists(path));
        Assert.False(Directory.Exists(Path.Combine(home.Root, "state")));
    }

    [Fact]
    public void AMissingOrRuinedRecordReadsAsNoRecord()
    {
        using InstallHome home = new();

        Assert.Null(InstallRecord.Read(home.Sub("nothing.json")));

        Directory.CreateDirectory(home.Root);
        File.WriteAllText(home.Sub("junk.json"), "{ this is not json");
        Assert.Null(InstallRecord.Read(home.Sub("junk.json")));
    }
}

public class PayloadTests
{
    [Fact]
    public void EveryFileOfABuildIsCopiedExceptTheSymbols()
    {
        using InstallHome home = new();
        string source = Path.GetDirectoryName(home.Build("download"))!;

        Assert.True(Payload.Install(source, home.LibDir, "Rove"));

        Assert.Equal("a binary", File.ReadAllText(Path.Combine(home.LibDir, "Rove")));
        Assert.Equal("skia a binary", File.ReadAllText(Path.Combine(home.LibDir, "libSkiaSharp.so")));
        Assert.True(File.Exists(Path.Combine(home.LibDir, "libHarfBuzzSharp.so")));
        Assert.False(File.Exists(Path.Combine(home.LibDir, "Rove.pdb")));
    }

    [Fact]
    public void AnUpdateReplacesEveryFileAndLeavesNoLitter()
    {
        using InstallHome home = new();
        Payload.Install(Path.GetDirectoryName(home.Build("old", "version one"))!, home.LibDir, "Rove");

        Assert.True(Payload.Install(Path.GetDirectoryName(home.Build("new", "version two"))!, home.LibDir, "Rove"));

        Assert.Equal("version two", File.ReadAllText(Path.Combine(home.LibDir, "Rove")));
        Assert.Equal("skia version two", File.ReadAllText(Path.Combine(home.LibDir, "libSkiaSharp.so")));
        Assert.Empty(Directory.EnumerateFiles(home.LibDir, "*.new"));
        Assert.Empty(Directory.EnumerateFiles(home.LibDir, "*.old"));
    }

    [Fact]
    public void RemovingTakesTheWholeFolder()
    {
        using InstallHome home = new();
        Payload.Install(Path.GetDirectoryName(home.Build("download"))!, home.LibDir, "Rove");

        Payload.Remove(home.LibDir);

        Assert.False(Directory.Exists(home.LibDir));
    }

    [Fact]
    public void NothingBesideTheProgramAndItsLibrariesIsTakenAlong()
    {
        using InstallHome home = new();
        string source = Path.GetDirectoryName(home.Build("download"))!;
        File.WriteAllText(Path.Combine(source, "tax-return.pdf"), "not ours");
        File.WriteAllText(Path.Combine(source, "someone-elses.exe"), "not ours either");
        File.WriteAllText(Path.Combine(source, "libVersioned.so.1.2"), "a versioned library");

        Assert.True(Payload.Install(source, home.LibDir, "Rove"));

        Assert.True(File.Exists(Path.Combine(home.LibDir, "Rove")));
        Assert.True(File.Exists(Path.Combine(home.LibDir, "libSkiaSharp.so")));
        Assert.True(File.Exists(Path.Combine(home.LibDir, "libVersioned.so.1.2")));
        Assert.False(File.Exists(Path.Combine(home.LibDir, "tax-return.pdf")));
        Assert.False(File.Exists(Path.Combine(home.LibDir, "someone-elses.exe")));
        Assert.False(File.Exists(Path.Combine(home.LibDir, "Rove.pdb")));
    }

    [Fact]
    public void AFolderWithNoProgramInItInstallsNothing()
    {
        using InstallHome home = new();
        string source = home.Sub("junk");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "libSkiaSharp.so"), "orphan");

        Assert.False(Payload.Install(source, home.LibDir, "Rove"));
    }

    [Fact]
    public void AnEmptySourceInstallsNothing()
    {
        using InstallHome home = new();
        string empty = home.Sub("empty");
        Directory.CreateDirectory(empty);

        Assert.False(Payload.Install(empty, home.LibDir, "Rove"));
    }
}

public class LinuxInstallTests
{
    [Fact]
    public void TheBuildLandsInItsOwnFolderAndTheDesktopPointsAtIt()
    {
        using InstallHome home = new();
        string downloaded = home.Build("download");

        string? installed = LinuxInstall.InstallPayload(home.LibDir, home.BinDir, downloaded);

        Assert.Equal(Path.Combine(home.LibDir, "Rove"), installed);
        Assert.Equal("a binary", File.ReadAllText(installed!));
        Assert.True(File.Exists(Path.Combine(home.LibDir, "libSkiaSharp.so")));

        LinuxInstall.InstallSupport(home.DataHome, home.BinDir, installed!, InstallHome.Asset);

        string entry = File.ReadAllText(Path.Combine(home.DataHome, "applications", "rove.desktop"));
        Assert.Contains($"Exec={LinuxInstall.QuoteExec(installed!)} %F", entry, StringComparison.Ordinal);
        Assert.Contains("StartupWMClass=rove", entry, StringComparison.Ordinal);
    }

    [Fact]
    public void TheNameOnPathLeadsToTheInstalledProgram()
    {
        if (!OperatingSystem.IsLinux())
            return;

        using InstallHome home = new();

        LinuxInstall.InstallPayload(home.LibDir, home.BinDir, home.Build("download"));

        string link = LinuxInstall.LinkPath(home.BinDir);
        Assert.True(File.Exists(link), link);
        Assert.Equal("a binary", File.ReadAllText(link));
    }

    [Fact]
    public void AnOlderInstallsBinaryOnPathGivesWayToTheLink()
    {
        if (!OperatingSystem.IsLinux())
            return;

        using InstallHome home = new();
        Directory.CreateDirectory(home.BinDir);
        File.WriteAllText(LinuxInstall.LinkPath(home.BinDir), "a whole binary from the old layout");

        string? installed = LinuxInstall.InstallPayload(home.LibDir, home.BinDir, home.Build("download"));

        Assert.Equal(Path.Combine(home.LibDir, "Rove"), installed);
        Assert.Equal("a binary", File.ReadAllText(LinuxInstall.LinkPath(home.BinDir)));
    }

    [Fact]
    public void EveryFileTheInstallPromisesIsActuallyWritten()
    {
        using InstallHome home = new();
        string installed = LinuxInstall.InstallPayload(home.LibDir, home.BinDir, home.Build("download"))!;

        LinuxInstall.InstallSupport(home.DataHome, home.BinDir, installed, InstallHome.Asset);

        foreach (string file in LinuxInstall.SupportFiles(home.DataHome, home.BinDir))
        {
            if (!OperatingSystem.IsLinux() && file == LinuxInstall.LinkPath(home.BinDir))
                continue;
            Assert.True(File.Exists(file), file);
        }
    }

    [Fact]
    public void UpdatingReplacesTheOlderBuildInPlace()
    {
        using InstallHome home = new();
        LinuxInstall.InstallPayload(home.LibDir, home.BinDir, home.Build("old", "version one"));

        string? installed = LinuxInstall.InstallPayload(home.LibDir, home.BinDir, home.Build("new", "version two"));

        Assert.Equal("version two", File.ReadAllText(installed!));
        if (OperatingSystem.IsLinux())
            Assert.Equal("version two", File.ReadAllText(LinuxInstall.LinkPath(home.BinDir)));
    }

    [Fact]
    public void InstallingTheBuildThatIsAlreadyInPlaceIsNotACopy()
    {
        using InstallHome home = new();
        Directory.CreateDirectory(home.LibDir);
        string inPlace = Path.Combine(home.LibDir, "Rove");
        File.WriteAllText(inPlace, "already here");

        string? installed = LinuxInstall.InstallPayload(home.LibDir, home.BinDir, inPlace);

        Assert.Equal(inPlace, installed);
        Assert.Equal("already here", File.ReadAllText(inPlace));
    }

    [Fact]
    public void ASecondLaunchWithNothingToChangeWritesNothing()
    {
        using InstallHome home = new();
        LinuxInstall.InstallSupport(home.DataHome, home.BinDir, "/home/me/.local/lib/rove/Rove", InstallHome.Asset);

        Assert.False(LinuxInstall.InstallSupport(
            home.DataHome, home.BinDir, "/home/me/.local/lib/rove/Rove", InstallHome.Asset));
    }

    [Fact]
    public void MovingTheBuildRewritesTheEntry()
    {
        using InstallHome home = new();
        LinuxInstall.InstallSupport(home.DataHome, home.BinDir, "/opt/rove/Rove", InstallHome.Asset);

        Assert.True(LinuxInstall.InstallSupport(
            home.DataHome, home.BinDir, "/home/me/.local/lib/rove/Rove", InstallHome.Asset));
    }

    [Fact]
    public void APackagedInstallOwnsTheEntry()
    {
        using InstallHome home = new();
        string system = home.Sub("usr", "share");
        Directory.CreateDirectory(Path.Combine(system, "applications"));
        File.WriteAllText(Path.Combine(system, "applications", "rove.desktop"), "[Desktop Entry]");

        Assert.True(LinuxInstall.OwnedBySystem([system]));
        Assert.False(LinuxInstall.OwnedBySystem([home.Sub("usr", "local", "share")]));
    }

    [Fact]
    public void UninstallingTakesBackEverythingItPutDown()
    {
        using InstallHome home = new();
        string installed = LinuxInstall.InstallPayload(home.LibDir, home.BinDir, home.Build("download"))!;
        LinuxInstall.InstallSupport(home.DataHome, home.BinDir, installed, InstallHome.Asset);

        LinuxInstall.Uninstall(home.DataHome, home.LibDir, home.BinDir);

        Assert.False(Directory.Exists(home.LibDir));
        foreach (string file in LinuxInstall.SupportFiles(home.DataHome, home.BinDir))
            Assert.False(File.Exists(file), file);
    }

    [Fact]
    public void APathWithSpacesIsQuotedForTheExecLine()
    {
        Assert.Equal("\"/home/me/My Apps/rove\"", LinuxInstall.QuoteExec("/home/me/My Apps/rove"));
        Assert.Equal("/opt/rove/rove", LinuxInstall.QuoteExec("/opt/rove/rove"));
        Assert.Equal("\"/opt/\\$odd/rove\"", LinuxInstall.QuoteExec("/opt/$odd/rove"));
    }
}

public class WindowsInstallTests
{
    [Fact]
    public void TheBuildLandsInItsProgramFolderAndGetsAStartMenuShortcut()
    {
        if (!OperatingSystem.IsWindows())
            return;

        using InstallHome home = new();
        string programDir = home.Sub("Programs", "Rove");
        string downloaded = home.Build("download", "a binary", "Rove.exe");

        string? installed = WindowsInstall.InstallPayload(programDir, downloaded);

        Assert.Equal(Path.Combine(programDir, "Rove.exe"), installed);
        Assert.Equal("a binary", File.ReadAllText(installed!));
        Assert.True(File.Exists(Path.Combine(programDir, "libSkiaSharp.so")));

        WindowsInstall.InstallSupport(home.StartMenu, installed!, "1.0.0", registerUninstall: false);

        Assert.True(File.Exists(WindowsInstall.ShortcutPath(home.StartMenu)));
    }

    [Fact]
    public void UpdatingReplacesTheBuildAndClearsTheOldOneAway()
    {
        if (!OperatingSystem.IsWindows())
            return;

        using InstallHome home = new();
        string programDir = home.Sub("Programs", "Rove");
        WindowsInstall.InstallPayload(programDir, home.Build("old", "version one", "Rove.exe"));

        string? installed = WindowsInstall.InstallPayload(programDir, home.Build("new", "version two", "Rove.exe"));

        Assert.Equal("version two", File.ReadAllText(installed!));
        Assert.Empty(Directory.EnumerateFiles(programDir, "*.old"));
        Assert.Empty(Directory.EnumerateFiles(programDir, "*.new"));
    }

    [Fact]
    public void UninstallingTakesBackTheBuildAndTheShortcut()
    {
        if (!OperatingSystem.IsWindows())
            return;

        using InstallHome home = new();
        string programDir = home.Sub("Programs", "Rove");
        string installed = WindowsInstall.InstallPayload(programDir, home.Build("download", "a binary", "Rove.exe"))!;
        WindowsInstall.InstallSupport(home.StartMenu, installed, "1.0.0", registerUninstall: false);

        WindowsInstall.Uninstall(programDir, home.StartMenu, unregister: false);

        Assert.False(Directory.Exists(programDir));
        Assert.False(File.Exists(WindowsInstall.ShortcutPath(home.StartMenu)));
    }
}
