using Rove.Core.Services;
using Xunit;

namespace Rove.Core.Tests;

public class XdgPathsTests
{
    [Fact]
    public void DataHomeFollowsTheEnvironmentVariableWhenSet()
    {
        if (!OperatingSystem.IsLinux())
            return;
        string? original = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        string chosen = Path.Combine(Path.GetTempPath(), "rove-xdg-test-data");
        try
        {
            Environment.SetEnvironmentVariable("XDG_DATA_HOME", chosen);
            Assert.Equal(chosen, XdgPaths.DataHome);
        }
        finally
        {
            Environment.SetEnvironmentVariable("XDG_DATA_HOME", original);
        }
    }

    [Fact]
    public void DataHomeFallsBackToTheDefaultWhenUnset()
    {
        if (!OperatingSystem.IsLinux())
            return;
        string? original = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        try
        {
            Environment.SetEnvironmentVariable("XDG_DATA_HOME", null);
            Assert.Equal(Path.Combine(XdgPaths.Home, ".local", "share"), XdgPaths.DataHome);
        }
        finally
        {
            Environment.SetEnvironmentVariable("XDG_DATA_HOME", original);
        }
    }

    [Fact]
    public void DataDirsAlwaysStartsWithDataHome()
    {
        if (!OperatingSystem.IsLinux())
            return;
        Assert.Equal(XdgPaths.DataHome, XdgPaths.DataDirs().First());
    }

    [Fact]
    public void ExistingDataDirsDropsWhatIsNotActuallyThereAndDeduplicates()
    {
        if (!OperatingSystem.IsLinux())
            return;
        string? original = Environment.GetEnvironmentVariable("XDG_DATA_DIRS");
        string real = Path.Combine(Path.GetTempPath(), "rove-xdg-test-realdir-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(real);
        try
        {
            Environment.SetEnvironmentVariable("XDG_DATA_DIRS", $"{real}:{real}:/no/such/rove-xdg-dir");
            string[] dirs = [.. XdgPaths.ExistingDataDirs()];

            Assert.Contains(real, dirs);
            Assert.DoesNotContain("/no/such/rove-xdg-dir", dirs);
            Assert.Equal(1, dirs.Count(d => d == real));
        }
        finally
        {
            Environment.SetEnvironmentVariable("XDG_DATA_DIRS", original);
            Directory.Delete(real);
        }
    }
}
