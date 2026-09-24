using Rove.UI.Services;
using Xunit;

namespace Rove.UI.Tests;

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
