using Rove.UI.Services;
using Xunit;

namespace Rove.UI.Tests;

public class InstallRecordTests
{
    [Fact]
    public void WhatWasWrittenComesBack()
    {
        using InstallHome home = new();
        string path = home.Sub("installed.json");
        BuildIdentity identity = new(new Version(1, 2, 3, 4), 4096, new DateTime(2026, 9, 9, 12, 0, 0, DateTimeKind.Utc));

        Assert.True(InstallRecord.For("/opt/rove/Rove", identity, ["/a", "/b"]).Write(path));
        InstallRecord? read = InstallRecord.Read(path);

        Assert.NotNull(read);
        Assert.Equal("/opt/rove/Rove", read.Binary);
        Assert.Equal(identity, read.Identity);
        Assert.Equal(["/a", "/b"], read.Files);
    }

    [Fact]
    public void DeletingTheRecordTakesItsFolderWithIt()
    {
        using InstallHome home = new();
        string path = Path.Combine(home.Root, "state", "installed.json");
        BuildIdentity identity = new(new Version(1, 0), 1, DateTime.UnixEpoch);
        InstallRecord.For("/opt/rove/Rove", identity, []).Write(path);

        InstallRecord.Delete(path);

        Assert.False(File.Exists(path));
        Assert.False(Directory.Exists(Path.Combine(home.Root, "state")));
    }

    [Fact]
    public void AMissingOrRuinedRecordReadsAsNoRecord()
    {
        using InstallHome home = new();

        Assert.Null(InstallRecord.Read(home.Sub("nothing.json")));

        Directory.CreateDirectory(home.Root);
        File.WriteAllText(home.Sub("junk.json"), "{ this is not json");
        Assert.Null(InstallRecord.Read(home.Sub("junk.json")));
    }
}
