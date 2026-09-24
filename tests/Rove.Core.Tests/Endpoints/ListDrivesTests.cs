using Rove.Core.Endpoints;
using Rove.Core.Protocol;
using Rove.Core.Services;
using Xunit;

namespace Rove.Core.Tests;

public class ListDrivesTests
{
    private readonly Actions _actions = new();

    [Fact]
    public void ListsAtLeastTheDriveTheTestsRunFrom()
    {
        CommandResult<DriveEntry[]> result = _actions.ListDrives(new());

        Assert.True(result.IsOk);
        Assert.NotEmpty(result.Data!);
        string root = Path.GetPathRoot(Path.GetFullPath(Directory.GetCurrentDirectory()))!;
        Assert.Contains(result.Data!, d => PathCompare.PathMatches(d.RootPath, root));
    }

    [Fact]
    public void EveryListedDriveRootIsAReadableDirectory()
    {
        CommandResult<DriveEntry[]> result = _actions.ListDrives(new());

        foreach (DriveEntry drive in result.Data!)
            Assert.True(Directory.Exists(drive.RootPath), drive.RootPath);
    }
}
