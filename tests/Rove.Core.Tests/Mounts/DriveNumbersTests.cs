using Rove.Core.Services;
using Xunit;

namespace Rove.Core.Tests;

public class DriveNumbersTests
{
    private static MountEntry Drive(string id) =>
        new(id, MountKind.Removable, "/dev/" + id, null, null, null,
            CanMount: true, CanUnmount: false, CanEject: true, VolumeId: id);

    private static int?[] Numbers(DriveNumbers numbers, params string[] ids) =>
        [.. numbers.Assign([.. ids.Select(Drive)]).Select(e => e.Number)];

    [Fact]
    public void DrivesAreNumberedFromOne()
    {
        Assert.Equal([1, 2], Numbers(new DriveNumbers(null), "a", "b"));
    }

    [Fact]
    public void AnImageCopyWithTheSameIdDoesNotTakeTheSticksNumber()
    {
        DriveNumbers numbers = new(null);
        Numbers(numbers, "other", "iso");
        MountEntry copy = Drive("iso") with { Kind = MountKind.Disk, Device = "/dev/loop0p1" };
        MountEntry stick = Drive("iso") with { MountUri = "file:///run/media/me/ISO", LocalPath = "/run/media/me/ISO" };

        MountEntry[] numbered = numbers.Assign([copy, stick]);

        Assert.Equal(2, numbered[1].Number);
        Assert.NotEqual(2, numbered[0].Number);
    }

    [Fact]
    public void ADriveKeepsItsNumberWhenPluggedInAlone()
    {
        DriveNumbers numbers = new(null);
        Numbers(numbers, "a", "b");

        Assert.Equal([2], Numbers(numbers, "b"));
    }

    [Fact]
    public void ANewDriveSkipsNumbersOthersRemember()
    {
        DriveNumbers numbers = new(null);
        Numbers(numbers, "a", "b");

        Assert.Equal([3], Numbers(numbers, "c"));
    }

    [Fact]
    public void AnEntryWithoutAVolumeIdGetsNoNumber()
    {
        MountEntry phone = Drive("p") with { Kind = MountKind.Phone, VolumeId = null };

        Assert.Null(Assert.Single(new DriveNumbers(null).Assign([phone])).Number);
    }

    [Fact]
    public void TwoDrivesWithTheSameIdStillGetDifferentNumbers()
    {
        Assert.Equal([1, 2], Numbers(new DriveNumbers(null), "a", "a"));
    }

    [Fact]
    public void NumbersAreRememberedInTheFile()
    {
        using TempDir temp = new();
        string file = Path.Combine(temp.Path, "state", "drive-numbers.txt");
        Numbers(new DriveNumbers(file), "a", "b");

        Assert.Equal([2], Numbers(new DriveNumbers(file), "b"));
    }
}
