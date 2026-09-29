using System.Text;
using Rove.Core.Services;
using Xunit;

namespace Rove.Core.Tests;

public class WindowsDriveListTests
{
    private static byte[] Descriptor(uint bus, string vendor, string product)
    {
        byte[] buffer = new byte[128];
        BitConverter.GetBytes(64u).CopyTo(buffer, 12);
        BitConverter.GetBytes(80u).CopyTo(buffer, 16);
        BitConverter.GetBytes(bus).CopyTo(buffer, 28);
        Encoding.ASCII.GetBytes(vendor).CopyTo(buffer, 64);
        Encoding.ASCII.GetBytes(product).CopyTo(buffer, 80);
        return buffer;
    }

    [Fact]
    public void AUsbDriveIsNamedByModelLabelAndLetter()
    {
        MountEntry entry = WindowsDriveList.Entry(new(@"E:\", "STICK", "SanDisk Cruzer", 0x1A2B3C4D, 16_000_000_000));

        Assert.Equal("SanDisk Cruzer (STICK, E:)", entry.Name);
        Assert.Equal(MountKind.Removable, entry.Kind);
        Assert.Equal(@"E:\", entry.LocalPath);
        Assert.Equal("1A2B-3C4D", entry.VolumeId);
        Assert.True(entry.IsMounted);
        Assert.True(entry.CanEject);
        Assert.False(entry.CanUnmount);
    }

    [Fact]
    public void WithoutAModelOrLabelTheSizeNamesIt()
    {
        Assert.Equal("STICK (F:)", WindowsDriveList.Entry(new(@"F:\", "STICK", null, 1, 1)).Name);
        Assert.Equal("16 GB Volume (F:)", WindowsDriveList.Entry(new(@"F:\", "", null, 1, 16_000_000_000)).Name);
    }

    [Fact]
    public void TheDescriptorSaysUsbAndGivesVendorAndProduct()
    {
        (bool usb, string? model) = WindowsDriveList.ReadDescriptor(Descriptor(7, "SanDisk ", "Cruzer Blade    "));

        Assert.True(usb);
        Assert.Equal("SanDisk Cruzer Blade", model);
    }

    [Fact]
    public void AnInternalDiskIsNotUsbAndATooShortDescriptorSaysNothing()
    {
        Assert.False(WindowsDriveList.ReadDescriptor(Descriptor(17, "", "Samsung SSD")).Usb);
        Assert.Equal((false, null), WindowsDriveList.ReadDescriptor(new byte[8]));
    }
}
