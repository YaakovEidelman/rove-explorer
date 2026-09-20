using Rove.Core.Protocol;
using Rove.Core.Services;
using Xunit;

namespace Rove.Core.Tests;

public class FileWatchServiceTests
{
    private static Task<List<IReadOnlyList<WatchEvent>>> CollectBatches(
        TempDir tmp, Action fireEvents, int expectedEvents, TimeSpan coalesceWindow
    ) => CollectBatches(tmp, fireEvents, expectedEvents, coalesceWindow, TimeSpan.FromMilliseconds(300));

    private static async Task<List<IReadOnlyList<WatchEvent>>> CollectBatches(
        TempDir tmp, Action fireEvents, int expectedEvents, TimeSpan coalesceWindow, TimeSpan idleWindow
    )
    {
        using FileWatchService watcher = new(coalesceWindow);
        List<IReadOnlyList<WatchEvent>> batches = [];
        object gate = new();
        TaskCompletionSource<bool> settled = new();
        Timer? idle = null;

        watcher.Subscribe(events =>
        {
            lock (gate)
            {
                batches.Add(events);
                int seen = 0;
                foreach (IReadOnlyList<WatchEvent> batch in batches)
                    seen += batch.Count;
                idle?.Dispose();
                idle = seen >= expectedEvents
                    ? new Timer(_ => settled.TrySetResult(true), null, idleWindow, Timeout.InfiniteTimeSpan)
                    : null;
            }
        }, () => { });
        watcher.ChangePath(tmp.Path);

        fireEvents();

        await settled.Task.WaitAsync(TimeSpan.FromSeconds(10));
        lock (gate)
            return batches;
    }

    [Fact]
    public async Task ABurstOfCreatesArrivesAsFarFewerBatchesThanFiles()
    {
        using TempDir tmp = new();
        const int fileCount = 200;

        List<IReadOnlyList<WatchEvent>> batches = await CollectBatches(
            tmp,
            () =>
            {
                for (int i = 0; i < fileCount; i++)
                    File.WriteAllText(Path.Combine(tmp.Path, $"f{i:D4}.txt"), "x");
            },
            fileCount,
            TimeSpan.FromMilliseconds(150));

        int totalUpserts = batches.Sum(b => b.Count(e => e is WatchEvent.Upserted));

        Assert.True(totalUpserts >= fileCount,
            $"expected at least {fileCount} upserts across batches, saw {totalUpserts}");
        Assert.True(batches.Count < fileCount / 4,
            $"expected far fewer than {fileCount} batches for {fileCount} files, saw {batches.Count}");
    }

    [Fact]
    public async Task ACreatedFileArrivesDescribedInTheBatch()
    {
        using TempDir tmp = new();

        List<IReadOnlyList<WatchEvent>> batches = await CollectBatches(
            tmp,
            () => File.WriteAllText(Path.Combine(tmp.Path, "hello.txt"), "hi"),
            expectedEvents: 1,
            TimeSpan.FromMilliseconds(30));

        List<WatchEvent.Upserted> upserts = [.. batches.SelectMany(b => b).OfType<WatchEvent.Upserted>()];
        Assert.NotEmpty(upserts);
        Assert.All(upserts, u => Assert.Equal("hello.txt", u.Item.Name));
    }

    [Fact]
    public async Task AFileDeletedBeforeTheBatchFlushesNeverArrivesAsIfItStillExisted()
    {
        using TempDir tmp = new();
        string path = Path.Combine(tmp.Path, "ghost.txt");

        List<IReadOnlyList<WatchEvent>> batches = await CollectBatches(
            tmp,
            () =>
            {
                File.WriteAllText(path, "x");
                File.Delete(path);
            },
            expectedEvents: 1,
            TimeSpan.FromMilliseconds(150),
            idleWindow: TimeSpan.FromSeconds(2));

        Assert.DoesNotContain(
            batches.SelectMany(b => b).OfType<WatchEvent.Upserted>(),
            u => u.Item.Name == "ghost.txt");
    }
}
