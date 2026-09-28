using Rove.Core.Protocol;
using Rove.Core.Services;
using Xunit;

namespace Rove.Core.Tests;

public class LinuxMountsTests
{
    private static MountEntry Entry(string name, MountKind kind) =>
        new(name, kind, Device: null, ActivationUri: null, MountUri: null, LocalPath: null,
            CanMount: true, CanUnmount: false, CanEject: false);

    [Fact]
    public async Task DrivesComeFromUDisksAndOnlyServersAndPhonesFromGio()
    {
        FakeMountService drives = new("udisks", Entry("STICK", MountKind.Removable));
        FakeMountService gio = new("gio",
            Entry("STICK", MountKind.Removable), Entry("Pixel", MountKind.Phone), Entry("nas", MountKind.Network));
        using LinuxMounts mounts = new(drives, gio);

        MountEntry[] all = await mounts.ListAsync(CancellationToken.None);

        Assert.Equal(["STICK", "Pixel", "nas"], all.Select(e => e.Name));
    }

    [Fact]
    public async Task EachEntryGoesToTheServiceThatOwnsIt()
    {
        FakeMountService drives = new("udisks");
        FakeMountService gio = new("gio");
        using LinuxMounts mounts = new(drives, gio);

        CommandResult<string> stick = await mounts.MountAsync(Entry("STICK", MountKind.Removable), null!, CancellationToken.None);
        CommandResult<string> phone = await mounts.MountAsync(Entry("Pixel", MountKind.Phone), null!, CancellationToken.None);
        await mounts.ConnectAsync("sftp://host", null!, CancellationToken.None);

        Assert.Equal("udisks", stick.Data);
        Assert.Equal("gio", phone.Data);
        Assert.Equal(["mount STICK"], drives.Calls);
        Assert.Equal(["mount Pixel", "connect sftp://host"], gio.Calls);
    }

    [Fact]
    public async Task WithoutGioServersSayWhy()
    {
        using LinuxMounts mounts = new(new FakeMountService("udisks"), remote: null);

        CommandResult<string> result = await mounts.ConnectAsync("sftp://host", null!, CancellationToken.None);

        Assert.False(result.IsOk);
        Assert.Contains("gio", result.Message);
    }

    [Fact]
    public void ChangesFromEitherServiceAreForwarded()
    {
        FakeMountService drives = new("udisks");
        FakeMountService gio = new("gio");
        using LinuxMounts mounts = new(drives, gio);
        int changes = 0;
        mounts.Changed += () => changes++;

        drives.RaiseChanged();
        gio.RaiseChanged();

        Assert.Equal(2, changes);
    }
}
