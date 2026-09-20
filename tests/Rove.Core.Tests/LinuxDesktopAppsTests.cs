using Rove.Core.Protocol;
using Rove.Core.Services;
using Xunit;

namespace Rove.Core.Tests;

public class LinuxDesktopAppsTests
{
    private static string Entry(string name, string exec, string extra = "") =>
        $"[Desktop Entry]\nType=Application\nName={name}\nExec={exec}\n{extra}";

    [Fact]
    public void AnAppThatTakesAFileIsListedByName()
    {
        using TempDir tmp = new();
        tmp.File("kate.desktop", Entry("Kate", "kate %U"));

        AppEntry[] apps = LinuxDesktopApps.Collect([tmp.Path]);

        AppEntry app = Assert.Single(apps);
        Assert.Equal("Kate", app.Name);
        Assert.Equal(tmp.Sub("kate.desktop"), app.DesktopFile);
    }

    [Fact]
    public void AppsAreSortedByNameIgnoringCase()
    {
        using TempDir tmp = new();
        tmp.File("b.desktop", Entry("beta", "b %f"));
        tmp.File("a.desktop", Entry("Zeta", "z %f"));
        tmp.File("c.desktop", Entry("Alpha", "a %F"));

        AppEntry[] apps = LinuxDesktopApps.Collect([tmp.Path]);

        Assert.Equal(["Alpha", "beta", "Zeta"], apps.Select(app => app.Name));
    }

    [Fact]
    public void AppsThatTakeNoFileAreLeftOut()
    {
        using TempDir tmp = new();
        tmp.File("clock.desktop", Entry("Clock", "clock"));

        Assert.Empty(LinuxDesktopApps.Collect([tmp.Path]));
    }

    [Fact]
    public void HiddenAndNonApplicationEntriesAreLeftOut()
    {
        using TempDir tmp = new();
        tmp.File("hidden.desktop", Entry("Hidden", "h %f", "NoDisplay=true\n"));
        tmp.File("gone.desktop", Entry("Gone", "g %f", "Hidden=true\n"));
        tmp.File("link.desktop", "[Desktop Entry]\nType=Link\nName=Link\nExec=l %f\n");

        Assert.Empty(LinuxDesktopApps.Collect([tmp.Path]));
    }

    [Fact]
    public void OnlyTheDesktopEntryGroupIsRead()
    {
        using TempDir tmp = new();
        tmp.File(
            "edit.desktop",
            "[Desktop Entry]\nType=Application\nName=Editor\nExec=edit %f\n"
                + "[Desktop Action new]\nName=New Window\nExec=edit --new\n");

        AppEntry app = Assert.Single(LinuxDesktopApps.Collect([tmp.Path]));

        Assert.Equal("Editor", app.Name);
    }

    [Fact]
    public void TheFirstFolderWinsWhenTwoHaveTheSameApp()
    {
        using TempDir user = new();
        using TempDir system = new();
        user.File("edit.desktop", Entry("My Editor", "edit %f"));
        system.File("edit.desktop", Entry("Editor", "edit %f"));

        AppEntry app = Assert.Single(LinuxDesktopApps.Collect([user.Path, system.Path]));

        Assert.Equal("My Editor", app.Name);
    }

    [Fact]
    public void AHiddenCopyInTheFirstFolderRemovesTheAppEntirely()
    {
        using TempDir user = new();
        using TempDir system = new();
        user.File("edit.desktop", Entry("Editor", "edit %f", "Hidden=true\n"));
        system.File("edit.desktop", Entry("Editor", "edit %f"));

        Assert.Empty(LinuxDesktopApps.Collect([user.Path, system.Path]));
    }

    [Fact]
    public void AFolderThatIsNotThereIsSkipped()
    {
        using TempDir tmp = new();
        tmp.File("kate.desktop", Entry("Kate", "kate %f"));

        AppEntry[] apps = LinuxDesktopApps.Collect([tmp.Sub("missing"), tmp.Path]);

        Assert.Single(apps);
    }
}
