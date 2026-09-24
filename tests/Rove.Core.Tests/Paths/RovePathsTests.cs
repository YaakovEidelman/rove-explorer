using Rove.Core.Services;
using Xunit;

namespace Rove.Core.Tests;

public class RovePathsTests
{
    [Fact]
    public void ConfigDirectoryEndsInRove()
    {
        Assert.EndsWith("rove", RovePaths.ConfigDirectory, StringComparison.Ordinal);
    }

    [Fact]
    public void KeybindingsAndBookmarksFilesLiveInTheConfigDirectory()
    {
        Assert.Equal(Path.Combine(RovePaths.ConfigDirectory, "keybindings.json"), RovePaths.KeybindingsFile);
        Assert.Equal(Path.Combine(RovePaths.ConfigDirectory, "bookmarks.json"), RovePaths.BookmarksFile);
    }

    [Fact]
    public void InstallRecordFileLivesInTheStateDirectory()
    {
        Assert.Equal(Path.Combine(RovePaths.StateDirectory, "installed.json"), RovePaths.InstallRecordFile);
    }

    [Fact]
    public void UpdateCheckStateFileLivesInTheStateDirectory()
    {
        Assert.Equal(Path.Combine(RovePaths.StateDirectory, "update-check.json"), RovePaths.UpdateCheckStateFile);
    }

    [Fact]
    public void ConfigDirectoryFollowsXdgConfigHomeOffWindows()
    {
        if (OperatingSystem.IsWindows())
            return;
        string? original = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        string chosen = Path.Combine(Path.GetTempPath(), "rove-xdg-test-config");
        try
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", chosen);
            Assert.Equal(Path.Combine(chosen, "rove"), RovePaths.ConfigDirectory);
        }
        finally
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", original);
        }
    }

    [Fact]
    public void StateDirectoryFollowsXdgStateHomeOffWindows()
    {
        if (OperatingSystem.IsWindows())
            return;
        string? original = Environment.GetEnvironmentVariable("XDG_STATE_HOME");
        string chosen = Path.Combine(Path.GetTempPath(), "rove-xdg-test-state");
        try
        {
            Environment.SetEnvironmentVariable("XDG_STATE_HOME", chosen);
            Assert.Equal(Path.Combine(chosen, "rove"), RovePaths.StateDirectory);
        }
        finally
        {
            Environment.SetEnvironmentVariable("XDG_STATE_HOME", original);
        }
    }
}
