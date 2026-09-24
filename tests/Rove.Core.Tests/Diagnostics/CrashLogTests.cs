using Rove.Core.Services;
using Xunit;

namespace Rove.Core.Tests;

public class CrashLogTests
{
    [Fact]
    public void WriteCreatesADailyFileWithTheExceptionInIt()
    {
        using TempDir dir = new();
        DateTimeOffset now = new(2026, 9, 11, 10, 0, 0, TimeSpan.Zero);

        CrashLog.Write(dir.Path, "Test", new InvalidOperationException("boom"), now);

        string path = dir.Sub("2026-09-11.log");
        Assert.True(File.Exists(path));
        string content = File.ReadAllText(path);
        Assert.Contains("Test", content, StringComparison.Ordinal);
        Assert.Contains("boom", content, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteAppendsRatherThanOverwriting()
    {
        using TempDir dir = new();
        DateTimeOffset now = new(2026, 9, 11, 10, 0, 0, TimeSpan.Zero);

        CrashLog.Write(dir.Path, "First", new Exception("one"), now);
        CrashLog.Write(dir.Path, "Second", new Exception("two"), now);

        string content = File.ReadAllText(dir.Sub("2026-09-11.log"));
        Assert.Contains("one", content, StringComparison.Ordinal);
        Assert.Contains("two", content, StringComparison.Ordinal);
    }

    [Fact]
    public void PruneDeletesLogsOlderThanRetention()
    {
        using TempDir dir = new();
        DateTimeOffset now = new(2026, 9, 11, 0, 0, 0, TimeSpan.Zero);
        dir.File("2026-08-01.log");
        string recent = dir.File("2026-09-01.log");
        string notALog = dir.File("notes.txt");

        CrashLog.Prune(dir.Path, now);

        Assert.False(File.Exists(dir.Sub("2026-08-01.log")));
        Assert.True(File.Exists(recent));
        Assert.True(File.Exists(notALog));
    }

    [Fact]
    public void PruneOnAMissingDirectoryDoesNothing()
    {
        using TempDir dir = new();
        CrashLog.Prune(dir.Sub("does-not-exist"), DateTimeOffset.Now);
    }
}
