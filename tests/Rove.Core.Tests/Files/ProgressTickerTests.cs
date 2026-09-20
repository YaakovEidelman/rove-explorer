using Rove.Core.Protocol;
using Rove.Core.Services;
using Xunit;

namespace Rove.Core.Tests;

public class ProgressTickerTests
{
    private sealed class Sink : IProgress<FileOpProgress>
    {
        public List<FileOpProgress> Reports { get; } = [];

        public void Report(FileOpProgress value) => Reports.Add(value);
    }

    [Fact]
    public void ReportsInsideTheIntervalAreDropped()
    {
        Sink sink = new();
        ProgressTicker ticker = new(sink);

        ticker.Report("Copying", 1, 10, "a.txt");
        ticker.Report("Copying", 2, 10, "b.txt");
        ticker.Report("Copying", 3, 10, "c.txt");

        Assert.Single(sink.Reports);
        Assert.Equal("a.txt", sink.Reports[0].CurrentItem);
    }

    [Fact]
    public void AnImportantReportAlwaysGetsThrough()
    {
        Sink sink = new();
        ProgressTicker ticker = new(sink);

        ticker.Report("Copying", 1, 10, "a.txt");
        ticker.Report("Copying", 10, 10, "done.txt", important: true);

        Assert.Equal(2, sink.Reports.Count);
        Assert.Equal("done.txt", sink.Reports[^1].CurrentItem);
    }

    [Fact]
    public void AReportArrivesAgainOnceTheIntervalPasses()
    {
        Sink sink = new();
        ProgressTicker ticker = new(sink);

        ticker.Report("Copying", 1, 10, "a.txt");
        Thread.Sleep(70);
        ticker.Report("Copying", 2, 10, "b.txt");

        Assert.Equal(2, sink.Reports.Count);
    }

    [Fact]
    public void ANullSinkIsFineToReportTo()
    {
        ProgressTicker ticker = new(null);
        ticker.Report("Copying", 1, 10, "a.txt");
    }
}
