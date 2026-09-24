using Rove.Core.Services;
using Xunit;

namespace Rove.Core.Tests;

public class UpdateCheckStateTests
{
    [Fact]
    public void WhatWasWrittenComesBack()
    {
        using TempDir dir = new();
        string path = dir.Sub("update-check.json");
        DateTime checkedAt = new(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc);

        Assert.True(new UpdateCheckState(checkedAt, "v1.2.3").Write(path));
        UpdateCheckState? read = UpdateCheckState.Read(path);

        Assert.NotNull(read);
        Assert.Equal(checkedAt, read.LastCheckedUtc);
        Assert.Equal("v1.2.3", read.SkippedVersion);
    }

    [Fact]
    public void AMissingOrRuinedStateReadsAsNeverChecked()
    {
        using TempDir dir = new();

        Assert.Null(UpdateCheckState.Read(dir.Sub("nothing.json")));

        string junk = dir.File("junk.json", "{ this is not json");
        Assert.Null(UpdateCheckState.Read(junk));
    }
}
