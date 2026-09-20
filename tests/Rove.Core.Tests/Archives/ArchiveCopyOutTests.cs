using Rove.Core.Endpoints;
using Rove.Core.Protocol;
using Xunit;

namespace Rove.Core.Tests;

public class ArchiveCopyOutTests
{
    private readonly Actions _actions = new();

    [Fact]
    public void AFileComesOutWhereSomethingElseCanOpenIt()
    {
        using TempDir tmp = new();
        ZipBuilder.Make(tmp.Sub("pack.zip"), ("sub/b.txt", "two"));

        CommandResult<string?> copy = _actions.CopyOutOfArchive(new(tmp.Sub("pack.zip", "sub", "b.txt")));

        Assert.True(copy.IsOk, copy.Message);
        Assert.Equal("two", File.ReadAllText(copy.Data!));
        Assert.Equal("b.txt", Path.GetFileName(copy.Data!));
    }

    [Fact]
    public void TheSameEntryComesBackToTheSamePlace()
    {
        using TempDir tmp = new();
        ZipBuilder.Make(tmp.Sub("pack.zip"), ("a.txt", "one"));

        string first = _actions.CopyOutOfArchive(new(tmp.Sub("pack.zip", "a.txt"))).Data!;
        string again = _actions.CopyOutOfArchive(new(tmp.Sub("pack.zip", "a.txt"))).Data!;

        Assert.Equal(first, again);
    }

    [Fact]
    public void TwoZipsOfTheSameNameDoNotShareACopy()
    {
        using TempDir tmp = new();
        tmp.Dir("left");
        tmp.Dir("right");
        ZipBuilder.Make(tmp.Sub("left", "pack.zip"), ("a.txt", "left"));
        ZipBuilder.Make(tmp.Sub("right", "pack.zip"), ("a.txt", "right"));

        string left = _actions.CopyOutOfArchive(new(tmp.Sub("left", "pack.zip", "a.txt"))).Data!;
        string right = _actions.CopyOutOfArchive(new(tmp.Sub("right", "pack.zip", "a.txt"))).Data!;

        Assert.NotEqual(left, right);
        Assert.Equal("left", File.ReadAllText(left));
        Assert.Equal("right", File.ReadAllText(right));
    }

    [Fact]
    public void ANewerZipReplacesTheCopyTakenFromTheOldOne()
    {
        using TempDir tmp = new();
        ZipBuilder.Make(tmp.Sub("pack.zip"), ("a.txt", "before"));
        string copy = _actions.CopyOutOfArchive(new(tmp.Sub("pack.zip", "a.txt"))).Data!;
        Assert.Equal("before", File.ReadAllText(copy));

        ZipBuilder.Make(tmp.Sub("pack.zip"), ("a.txt", "after"));
        File.SetLastWriteTimeUtc(tmp.Sub("pack.zip"), DateTime.UtcNow.AddMinutes(1));

        string again = _actions.CopyOutOfArchive(new(tmp.Sub("pack.zip", "a.txt"))).Data!;

        Assert.Equal(copy, again);
        Assert.Equal("after", File.ReadAllText(again));
    }

    [Fact]
    public void AskingForAnEntryThatIsGoneSaysSo()
    {
        using TempDir tmp = new();
        ZipBuilder.Make(tmp.Sub("pack.zip"), ("a.txt", "one"));

        CommandResult<string?> copy = _actions.CopyOutOfArchive(new(tmp.Sub("pack.zip", "nope.txt")));

        Assert.False(copy.IsOk);
        Assert.Equal("not_found", copy.Reason);
    }

    [Fact]
    public void AskingForSomethingThatIsNotInAZipIsRefused()
    {
        using TempDir tmp = new();
        tmp.File("loose.txt", "one");

        CommandResult<string?> copy = _actions.CopyOutOfArchive(new(tmp.Sub("loose.txt")));

        Assert.False(copy.IsOk);
        Assert.Equal("not_in_archive", copy.Reason);
    }

    [Fact]
    public void NothingCanBeLaunchedWhileItIsStillInsideAZip()
    {
        using TempDir tmp = new();
        ZipBuilder.Make(tmp.Sub("pack.zip"), ("a.txt", "one"));

        CommandResult<string?> launched = _actions.LaunchFile(new(tmp.Sub("pack.zip", "a.txt")));

        Assert.False(launched.IsOk);
        Assert.Equal("in_archive", launched.Reason);
    }
}
