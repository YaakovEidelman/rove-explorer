using Rove.Core.Protocol;
using Rove.Core.Services;
using Xunit;

namespace Rove.Core.Tests;

public class DriveFilterTests
{
    [Theory]
    [InlineData("/proc", "proc")]
    [InlineData("/sys/fs/cgroup", "cgroup2")]
    [InlineData("/dev/shm", "tmpfs")]
    [InlineData("/run/lock", "tmpfs")]
    [InlineData("/run/user/1000", "tmpfs")]
    [InlineData("/snap/firefox/4173", "squashfs")]
    [InlineData("/var/snap/lxd/common/ns", "nsfs")]
    [InlineData("/var/lib/docker/overlay2/abc/merged", "overlay")]
    [InlineData("/boot/efi", "vfat")]
    [InlineData("/sys/kernel/debug", "debugfs")]
    public void HidesTheSystemsOwnPlumbing(string mount, string filesystem)
    {
        Assert.True(DriveFilter.IsSystemMount(mount, filesystem), mount);
    }

    [Theory]
    [InlineData("/boot", "ext4")]
    [InlineData("/boot/grub2/i386-pc", "btrfs")]
    [InlineData("/var/log", "btrfs")]
    [InlineData("/var/lib/flatpak", "ext4")]
    [InlineData("/usr", "ext4")]
    [InlineData("/usr/lib/wsl/drivers", "9p")]
    [InlineData("/etc", "ext4")]
    [InlineData("/sysroot", "ext4")]
    [InlineData("/nix/store", "ext4")]
    public void HidesTheSystemsOwnFurniture(string mount, string filesystem)
    {
        Assert.True(DriveFilter.IsSystemMount(mount, filesystem), mount);
    }

    [Theory]
    [InlineData("/.snapshots", "btrfs")]
    [InlineData("/tmp/.mount_roveAb12", "fuse")]
    [InlineData("/home/yeide/.cache/doc", "fuse.portal")]
    public void HidesWhatWasMountedOutOfTheWay(string mount, string filesystem)
    {
        Assert.True(DriveFilter.IsSystemMount(mount, filesystem), mount);
    }

    [Theory]
    [InlineData("/mnt/wsl", "tmpfs")]
    [InlineData("/mnt/wslg", "tmpfs")]
    [InlineData("/media/scratch", "tmpfs")]
    public void ScratchSpaceIsScratchSpaceWhereverItIsMounted(string mount, string filesystem)
    {
        Assert.True(DriveFilter.IsSystemMount(mount, filesystem), mount);
    }

    [Theory]
    [InlineData("/", "ext4")]
    [InlineData("/home", "ext4")]
    [InlineData("/data", "btrfs")]
    [InlineData("/mnt/backup", "xfs")]
    [InlineData("/media/yeide/USB STICK", "vfat")]
    [InlineData("/run/media/yeide/Photos", "exfat")]
    [InlineData("/srv/share", "nfs")]
    [InlineData("/mnt/c", "drvfs")]
    [InlineData("/media/yeide/Fedora-Live", "iso9660")]
    [InlineData("/mnt/photos", "fuseblk")]
    [InlineData("/opt/data", "ext4")]
    [InlineData("/home/yeide/remote", "fuse.sshfs")]
    public void KeepsAnywhereWorthOpening(string mount, string filesystem)
    {
        Assert.False(DriveFilter.IsSystemMount(mount, filesystem), mount);
    }

    [Fact]
    public void RootSurvivesEvenWhenItsFilesystemLooksLikePlumbing()
    {
        // A live-USB session really does run its root off a squashfs image.
        Assert.False(DriveFilter.IsSystemMount("/", "squashfs"));
    }

    [Fact]
    public void AMountThatWontNameItsFilesystemIsJudgedOnItsPathAlone()
    {
        Assert.True(DriveFilter.IsSystemMount("/proc", null));
        Assert.False(DriveFilter.IsSystemMount("/mnt/disk", null));
    }

    [Theory]
    [InlineData("/mnt/")]
    [InlineData("/media/yeide/USB/")]
    public void TrailingSlashesDoNotChangeTheAnswer(string mount)
    {
        Assert.False(DriveFilter.IsSystemMount(mount, "vfat"));
    }

    [Fact]
    public void APathThatMerelyStartsWithASystemNameIsNotHidden()
    {
        Assert.False(DriveFilter.IsSystemMount("/development", "ext4"));
        Assert.False(DriveFilter.IsSystemMount("/snapshots", "ext4"));
        Assert.False(DriveFilter.IsSystemMount("/vars", "ext4"));
        Assert.False(DriveFilter.IsSystemMount("/etcetera", "ext4"));
    }

    [Fact]
    public void TheSamePlaceMountedTwiceIsListedOnce()
    {
        DriveEntry[] kept = DriveFilter.Deduplicate([
            new("/", null, "Fixed", 100, 50),
            new("/", "overlay", "Fixed", 100, 50),
            new("/home", null, "Fixed", 100, 50),
        ]);

        Assert.Equal(2, kept.Length);
        Assert.Equal(["/", "/home"], kept.Select(d => d.RootPath));
    }

    [Fact]
    public void ATrailingSlashIsNotADifferentPlace()
    {
        DriveEntry[] kept = DriveFilter.Deduplicate([
            new("/media/usb", null, "Removable", 100, 50),
            new("/media/usb/", null, "Removable", 100, 50),
        ]);

        Assert.Single(kept);
    }
}
