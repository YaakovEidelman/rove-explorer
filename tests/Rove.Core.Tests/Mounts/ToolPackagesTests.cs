using Rove.Core.Services;
using Xunit;

namespace Rove.Core.Tests;

public class ToolPackagesTests
{
    [Fact]
    public void ArchInstallsUDisksWithPacman()
    {
        Assert.Equal(
            ["sudo", "pacman", "-S", "--needed", "udisks2", "polkit"],
            ToolPackages.Command("NAME=\"Arch Linux\"\nID=arch\n", MountTool.UDisks));
    }

    [Fact]
    public void ADistroBasedOnAnotherUsesItsParentsPackages()
    {
        Assert.Equal(
            ["sudo", "pacman", "-S", "--needed", "udisks2", "polkit"],
            ToolPackages.Command("ID=manjaro\nID_LIKE=arch\n", MountTool.UDisks));
        Assert.Equal(
            ["sudo", "apt-get", "install", "libglib2.0-bin", "gvfs", "gvfs-backends"],
            ToolPackages.Command("ID=pop\nID_LIKE=\"ubuntu debian\"\n", MountTool.Gio));
    }

    [Fact]
    public void OpenSuseVariantsUseZypper()
    {
        Assert.Equal(
            ["sudo", "zypper", "install", "udisks2"],
            ToolPackages.Command("ID=\"opensuse-tumbleweed\"\nID_LIKE=\"opensuse suse\"\n", MountTool.UDisks));
    }

    [Fact]
    public void AnUnknownDistroHasNoCommand()
    {
        Assert.Null(ToolPackages.Command("ID=gentoo\n", MountTool.UDisks));
        Assert.Null(ToolPackages.Command("", MountTool.UDisks));
    }

    [Fact]
    public void SteamOsCountsAsReadOnly()
    {
        Assert.True(ToolPackages.IsReadOnlySystem("ID=steamos\nID_LIKE=arch\n"));
        Assert.False(ToolPackages.IsReadOnlySystem("ID=arch\n"));
    }
}
