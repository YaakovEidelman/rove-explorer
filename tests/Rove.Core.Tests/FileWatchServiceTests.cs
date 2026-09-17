using Rove.Core.Protocol;
using Rove.Core.Services;
using Xunit;

namespace Rove.Core.Tests;

/// <summary>
/// Real <see cref="FileSystemWatcher"/> events, on a real temp folder — the
/// same trick the trash tests use for their own OS-backed format, applied
/// here to the coalescing that used to be missing.
/// </summary>
public class FileWatchServiceTests
{
    private static Task<List<IReadOnlyList<WatchEvent>>> CollectBatches(
        TempDir tmp, Action fireEvents, int expectedEvents, TimeSpan coalesceWindow
    ) => CollectBatches(tmp, fireEvents, expectedEvents, coalesceWindow, TimeSpan.FromMilliseconds(300));

    // Settling on "N events, then a short quiet period" is a race against the OS's own
    // notification latency: under a loaded CI runner, a later event (e.g. a delete
    // following a create) can be delivered after the quiet period already declared the
    // batch done. Callers verifying that a later event changes the outcome need a wider
    // quiet period so the real, but delayed, event still arrives in time.
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

        // Writing a file raises Created and Changed both — one call, but not
        // necessarily one raw event. Every one of them has to describe it right.
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
