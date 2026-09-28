using Rove.Core.Services;
using Xunit;

namespace Rove.Core.Tests;

public class UDisksMountListTests
{
    private const string Home = "/home/me";
    private const string BlockRoot = "/org/freedesktop/UDisks2/block_devices/";
    private const string UsbDrive = "/org/freedesktop/UDisks2/drives/SanDisk";
    private const string InternalDrive = "/org/freedesktop/UDisks2/drives/Nvme";

    private static readonly UDisksDrive[] _drives =
    [
        new(UsbDrive, Removable: true, Ejectable: true, CanPowerOff: true),
        new(InternalDrive, Removable: false, Ejectable: false, CanPowerOff: false),
    ];

    private static UDisksBlock Block(
        string name,
        string drive = UsbDrive,
        string label = "",
        string[]? mountPoints = null,
        bool filesystem = true,
        bool encrypted = false,
        bool ignore = false,
        string? backing = null,
        string uuid = "") =>
        new(BlockRoot + name, "/dev/" + name, label, 16_000_000_000, backing is null ? drive : null, ignore,
            filesystem, mountPoints ?? [], encrypted, backing is null ? null : BlockRoot + backing, uuid);

    private static MountEntry[] Build(params UDisksBlock[] blocks) =>
        UDisksMountList.Build(new UDisksObjects(blocks, _drives), Home);

    [Fact]
    public void AnUnpluggedInStickCanBeOpenedAndEjected()
    {
        MountEntry stick = Assert.Single(Build(Block("sdb1", label: "STICK")));

        Assert.Equal("STICK", stick.Name);
        Assert.Equal(MountKind.Removable, stick.Kind);
        Assert.Equal("/dev/sdb1", stick.Device);
        Assert.False(stick.IsMounted);
        Assert.True(stick.CanMount);
        Assert.True(stick.CanEject);
    }

    [Fact]
    public void AMountedStickPointsAtItsFolder()
    {
        MountEntry stick = Assert.Single(Build(Block("sdb1", label: "STICK", mountPoints: ["/run/media/me/My Stick"])));

        Assert.Equal("/run/media/me/My Stick", stick.LocalPath);
        Assert.Equal("file:///run/media/me/My%20Stick", stick.MountUri);
        Assert.True(stick.CanUnmount);
        Assert.False(stick.CanMount);
    }

    [Fact]
    public void SystemMountsIgnoredBlocksAndBareDisksAreHidden()
    {
        MountEntry[] entries = Build(
            Block("nvme0n1", InternalDrive, filesystem: false),
            Block("nvme0n1p1", InternalDrive, mountPoints: ["/boot"], ignore: true),
            Block("nvme0n1p3", InternalDrive, mountPoints: ["/", "/home"]),
            Block("nvme0n1p4", InternalDrive, label: "Data", mountPoints: [Home + "/data"]));

        MountEntry data = Assert.Single(entries);
        Assert.Equal("Data", data.Name);
        Assert.Equal(MountKind.Disk, data.Kind);
        Assert.False(data.CanEject);
    }

    [Fact]
    public void AnUnnamedVolumeIsNamedBySize()
    {
        Assert.Equal("16 GB Volume", Assert.Single(Build(Block("sdb1"))).Name);
    }

    [Fact]
    public void ALockedDriveShowsOnceAsEncrypted()
    {
        MountEntry locked = Assert.Single(Build(Block("sdb1", filesystem: false, encrypted: true)));

        Assert.Equal("16 GB Encrypted", locked.Name);
        Assert.True(locked.CanMount);
    }

    [Fact]
    public void AnUnlockedDriveShowsItsInsideButKeepsTheOuterDevice()
    {
        MountEntry[] entries = Build(
            Block("sdb1", filesystem: false, encrypted: true),
            Block("dm-1", label: "SECRET", mountPoints: ["/run/media/me/SECRET"], backing: "sdb1"));

        MountEntry drive = Assert.Single(entries);
        Assert.Equal("SECRET", drive.Name);
        Assert.Equal("/dev/sdb1", drive.Device);
        Assert.Equal(MountKind.Removable, drive.Kind);
        Assert.Equal("/run/media/me/SECRET", drive.LocalPath);
    }

    [Fact]
    public void AnEncryptedDriveKeepsTheSameIdLockedOrUnlocked()
    {
        UDisksBlock outer = Block("sdb1", filesystem: false, encrypted: true, uuid: "outer");
        UDisksBlock inner = Block("dm-1", label: "SECRET", backing: "sdb1", uuid: "inner");

        Assert.Equal("outer", Assert.Single(Build(outer)).VolumeId);
        Assert.Equal("outer", Assert.Single(Build(outer, inner)).VolumeId);
    }

    [Fact]
    public void TheUnlockedSystemDiskStaysHidden()
    {
        Assert.Empty(Build(
            Block("nvme0n1p2", InternalDrive, filesystem: false, encrypted: true),
            Block("dm-0", mountPoints: ["/", "/home"], backing: "nvme0n1p2")));
    }

    [Theory]
    [InlineData(0UL, "0 bytes")]
    [InlineData(999UL, "999 bytes")]
    [InlineData(1_500_000UL, "1.5 MB")]
    [InlineData(15_600_000_000UL, "16 GB")]
    [InlineData(2_000_000_000_000UL, "2 TB")]
    public void SizesUseDecimalUnits(ulong bytes, string text)
    {
        Assert.Equal(text, UDisksMountList.SizeText(bytes));
    }
}
