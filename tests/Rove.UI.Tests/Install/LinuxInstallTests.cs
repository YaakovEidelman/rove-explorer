using Rove.UI.Services;
using Xunit;

namespace Rove.UI.Tests;

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
