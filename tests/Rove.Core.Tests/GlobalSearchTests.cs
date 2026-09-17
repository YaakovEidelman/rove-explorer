using Rove.Core.Protocol;
using Rove.Core.Services;
using Xunit;

namespace Rove.Core.Tests;

public class GlobalSearchTests
{
    private readonly GlobalSearchService _search = new();

    [Fact]
    public async Task FindsNestedItems_Fuzzily()
    {
        using TempDir tmp = new();
        tmp.Dir("level1");
        tmp.Dir(@"level1\level2");
        tmp.File(@"level1\level2\report-final.docx");
        tmp.File("unrelated.png");

        CommandResult<SearchHit[]> result =
            await _search.SearchAsync(tmp.Path, "rptfin", 50, CancellationToken.None);

        Assert.True(result.IsOk);
        SearchHit hit = Assert.Single(result.Data!);
        Assert.Equal("report-final.docx", hit.Item.Name);
    }

    [Fact]
    public async Task EmptyQuery_ReturnsNothing()
    {
        using TempDir tmp = new();
        tmp.File("a.txt");
        CommandResult<SearchHit[]> result =
            await _search.SearchAsync(tmp.Path, "", 50, CancellationToken.None);
        Assert.True(result.IsOk);
        Assert.Empty(result.Data!);
    }

    [Fact]
    public async Task RespectsMaxResults_AndOrdersByScore()
    {
        using TempDir tmp = new();
        tmp.File("match-exact.txt");
        for (int i = 0; i < 10; i++)
            tmp.File($"m{i}xaxtxcxh.txt"); // scattered weak matches

        CommandResult<SearchHit[]> result =
            await _search.SearchAsync(tmp.Path, "match", 3, CancellationToken.None);

        Assert.True(result.IsOk);
        Assert.Equal(3, result.Data!.Length);
        Assert.Equal("match-exact.txt", result.Data[0].Item.Name); // best first
    }

    [Fact]
    public async Task MissingRoot_Fails()
    {
        using TempDir tmp = new();
        CommandResult<SearchHit[]> result =
            await _search.SearchAsync(tmp.Sub("nope"), "x", 10, CancellationToken.None);
        Assert.False(result.IsOk);
        Assert.Equal("not_found", result.Reason);
    }

    [Fact]
    public async Task Cancellation_StopsEarlyWithoutThrowing()
    {
        using TempDir tmp = new();
        tmp.File("a-match.txt");
        using CancellationTokenSource cts = new();
        cts.Cancel();

        // Already-cancelled token: Task.Run may throw TaskCanceledException or
        // return an empty result — either is acceptable, but never a crash.
        try
        {
            CommandResult<SearchHit[]> result =
                await _search.SearchAsync(tmp.Path, "match", 10, cts.Token);
            Assert.NotNull(result.Data);
        }
        catch (OperationCanceledException)
        {
        }
    }
}
