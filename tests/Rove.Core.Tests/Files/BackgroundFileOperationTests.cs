using Rove.Core.Endpoints;
using Rove.Core.Protocol;
using Xunit;

namespace Rove.Core.Tests;

public class BackgroundFileOperationTests
{
    private readonly Actions _actions = new();

    [Fact]
    public async Task CopyReportsProgressForEveryItem()
    {
        using TempDir tmp = new();
        string target = tmp.Dir("target");
        string[] sources = [tmp.File("a.txt", "a"), tmp.File("b.txt", "b"), tmp.File("c.txt", "c")];
        ProgressLog log = new();

        CommandResult<OpResult[]> result =
            await _actions.CopyItemsAsync(new(sources, target, Overwrite: false), log);

        Assert.True(result.IsOk, result.Message);
        Assert.Equal(3, result.Data!.Length);
        Assert.Equal(3, log.Reports[^1].Completed);
        Assert.Equal(3, log.Reports[^1].Total);
        Assert.Equal(1, log.Reports[^1].Fraction);
        Assert.Contains(log.Reports, r => r.CurrentItem == "a.txt");
    }

    [Fact]
    public async Task CopyStopsWhenCancelledAndSaysSo()
    {
        using TempDir tmp = new();
        string target = tmp.Dir("target");
        string[] sources = [tmp.File("a.txt", "a"), tmp.File("b.txt", "b"), tmp.File("c.txt", "c")];

        using CancellationTokenSource cts = new();
        cts.Cancel();

        CommandResult<OpResult[]> result =
            await _actions.CopyItemsAsync(new(sources, target, Overwrite: false), null, cts.Token);

        Assert.False(result.IsOk);
        Assert.Equal("cancelled", result.Reason);
        Assert.All(result.Data!, op => Assert.Equal("cancelled", op.Reason));
        Assert.Empty(Directory.GetFiles(target));
    }

    [Fact]
    public async Task CancellingPartWayLeavesTheItemsAlreadyDone()
    {
        using TempDir tmp = new();
        string target = tmp.Dir("target");
        string[] sources = [tmp.File("a.txt", "a"), tmp.File("b.txt", "b"), tmp.File("c.txt", "c")];

        using CancellationTokenSource cts = new();
        ProgressCallback progress = new(p =>
        {
            if (p.Completed >= 1)
                cts.Cancel();
        });

        CommandResult<OpResult[]> result =
            await _actions.CopyItemsAsync(new(sources, target, Overwrite: false), progress, cts.Token);

        Assert.False(result.IsOk);
        Assert.Equal("partial_failure", result.Reason);
        Assert.True(result.Data!.Count(op => op.Ok) >= 1);
        Assert.Contains(result.Data!, op => op.Reason == "cancelled");
    }

    [Fact]
    public async Task CancelledFolderCopyDoesNotLeaveAHalfCopiedFolder()
    {
        using TempDir tmp = new();
        string target = tmp.Dir("target");
        string source = tmp.Dir("source");
        for (int i = 0; i < 200; i++)
            File.WriteAllText(Path.Combine(source, $"f{i}.txt"), new string('x', 2048));

        using CancellationTokenSource cts = new();
        ProgressCallback progress = new(_ => cts.Cancel());

        CommandResult<OpResult[]> result =
            await _actions.CopyItemsAsync(new([source], target, Overwrite: false), progress, cts.Token);

        Assert.False(result.IsOk);
        Assert.Equal("cancelled", result.Reason);
        Assert.False(Directory.Exists(Path.Combine(target, "source")));
    }

    [Fact]
    public async Task PermanentDeleteRunsInTheBackgroundAndReports()
    {
        using TempDir tmp = new();
        string[] paths = [tmp.File("a.txt", "a"), tmp.File("b.txt", "b")];
        ProgressLog log = new();

        CommandResult<OpResult[]> result = await _actions.DeleteItemsPermanentAsync(new(paths), log);

        Assert.True(result.IsOk, result.Message);
        Assert.All(paths, p => Assert.False(File.Exists(p)));
        Assert.Equal(2, log.Reports[^1].Completed);
    }

    [Fact]
    public async Task MoveReportsProgressAndMovesEverything()
    {
        using TempDir tmp = new();
        string target = tmp.Dir("target");
        string[] sources = [tmp.File("a.txt", "a"), tmp.File("b.txt", "b")];
        ProgressLog log = new();

        CommandResult<OpResult[]> result =
            await _actions.MoveItemsAsync(new(sources, target, Overwrite: false), log);

        Assert.True(result.IsOk, result.Message);
        Assert.All(sources, p => Assert.False(File.Exists(p)));
        Assert.Equal(2, Directory.GetFiles(target).Length);
        Assert.Equal("Moving", log.Reports[0].Verb);
    }
}
