using Rove.Core.Services;
using Xunit;

namespace Rove.Core.Tests;

public class GioMountListTests
{
    private const string InternalDisk = """
        Drive(0): SKHynix_HFS256GEJ4X112N
          Type: GProxyDrive (GProxyVolumeMonitorUDisks2)
          ids:
           unix-device: '/dev/nvme0n1'
          themed icons:  [drive-harddisk-solidstate]  [drive-harddisk]
          is_removable=0
          can_eject=0
        """;

    private const string UsbStickMounted = """
        Drive(1): SanDisk Cruzer
          Type: GProxyDrive (GProxyVolumeMonitorUDisks2)
          ids:
           unix-device: '/dev/sdb'
          is_removable=1
          can_eject=1
          Volume(0): STICK
            Type: GProxyVolume (GProxyVolumeMonitorUDisks2)
            ids:
             class: 'device'
             unix-device: '/dev/sdb1'
             uuid: '1234-ABCD'
             label: 'STICK'
            uuid=1234-ABCD
            can_mount=1
            can_eject=1
            should_automount=1
            Mount(0): STICK -> file:///run/media/me/My%20Stick
              Type: GProxyShadowMount (GProxyVolumeMonitorUDisks2)
              default_location=file:///run/media/me/My%20Stick
              can_unmount=1
              can_eject=1
              is_shadowed=0
        """;

    private const string UsbStickUnmounted = """
        Drive(1): SanDisk Cruzer
          Type: GProxyDrive (GProxyVolumeMonitorUDisks2)
          is_removable=1
          can_eject=1
          Volume(0): STICK
            Type: GProxyVolume (GProxyVolumeMonitorUDisks2)
            ids:
             unix-device: '/dev/sdb1'
            can_mount=1
            can_eject=1
        """;

    private const string PhoneAndServer = """
        Volume(0): Pixel 8
          Type: GProxyVolume (GProxyVolumeMonitorMTP)
          ids:
           unix-device: '/dev/bus/usb/003/007'
          activation_root=mtp://Google_Pixel_8_ABC/
          can_mount=1
          can_eject=0
        Mount(0): ftp.gnu.org -> ftp://ftp.gnu.org/
          Type: GDaemonMount
          default_location=ftp://ftp.gnu.org/
          can_unmount=1
          can_eject=0
          is_shadowed=0
        """;

    [Fact]
    public void ADriveWithNoVolumesListsNothing()
    {
        Assert.Empty(GioMountList.Parse(InternalDisk));
    }

    [Fact]
    public void AMountedUsbStickIsRemovableWithItsFolder()
    {
        MountEntry stick = Assert.Single(GioMountList.Parse(UsbStickMounted));

        Assert.Equal("STICK", stick.Name);
        Assert.Equal(MountKind.Removable, stick.Kind);
        Assert.Equal("/dev/sdb1", stick.Device);
        Assert.Equal("/run/media/me/My Stick", stick.LocalPath);
        Assert.True(stick.IsMounted);
        Assert.True(stick.IsFileBacked);
        Assert.True(stick.CanEject);
        Assert.True(stick.CanUnmount);
    }

    [Fact]
    public void AnUnmountedUsbStickCanBeMountedByDevice()
    {
        MountEntry stick = Assert.Single(GioMountList.Parse(UsbStickUnmounted));

        Assert.False(stick.IsMounted);
        Assert.True(stick.CanMount);
        Assert.Equal("/dev/sdb1", stick.Device);
        Assert.Equal(MountKind.Removable, stick.Kind);
    }

    [Fact]
    public void APhoneIsMountedThroughItsActivationRoot()
    {
        MountEntry phone = GioMountList.Parse(PhoneAndServer)[0];

        Assert.Equal(MountKind.Phone, phone.Kind);
        Assert.Equal("mtp://Google_Pixel_8_ABC/", phone.ActivationUri);
        Assert.False(phone.IsMounted);
    }

    [Fact]
    public void AServerMountWithNoVolumeIsNetwork()
    {
        MountEntry server = GioMountList.Parse(PhoneAndServer)[1];

        Assert.Equal(MountKind.Network, server.Kind);
        Assert.Equal("ftp.gnu.org", server.Name);
        Assert.Equal("ftp://ftp.gnu.org/", server.MountUri);
        Assert.Null(server.LocalPath);
        Assert.False(server.IsFileBacked);
    }

    [Fact]
    public void AShadowedMountIsLeftOut()
    {
        string shadowed = PhoneAndServer.Replace("is_shadowed=0", "is_shadowed=1");

        Assert.Single(GioMountList.Parse(shadowed));
    }

    [Fact]
    public void TheSameVolumeIsFoundAgainAfterItMounts()
    {
        MountEntry before = Assert.Single(GioMountList.Parse(UsbStickUnmounted));
        MountEntry after = Assert.Single(GioMountList.Parse(UsbStickMounted));

        Assert.True(after.SameVolume(before));
    }
}
