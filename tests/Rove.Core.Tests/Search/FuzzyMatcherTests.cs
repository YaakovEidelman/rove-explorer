using Rove.Core.Services;
using Xunit;

namespace Rove.Core.Tests;

public class FuzzyMatcherTests
{
    [Fact]
    public void EmptyQuery_MatchesEverything()
    {
        Assert.True(FuzzyMatcher.TryMatch("", "anything.txt", out int score));
        Assert.Equal(0, score);
    }

    [Theory]
    [InlineData("doc", "Documents")]
    [InlineData("DOC", "documents")]
    [InlineData("rvm", "RoveViewModel.cs")]
    [InlineData("mwvm", "MainWindowViewModel.cs")]
    public void SubsequenceInOrder_Matches(string query, string candidate)
    {
        Assert.True(FuzzyMatcher.TryMatch(query, candidate, out _));
    }

    [Theory]
    [InlineData("xyz", "Documents")]
    [InlineData("cod", "doc")]
    [InlineData("a", "")]
    public void MissingOrOutOfOrder_DoesNotMatch(string query, string candidate)
    {
        Assert.False(FuzzyMatcher.TryMatch(query, candidate, out _));
    }

    [Fact]
    public void PrefixMatch_ScoresHigherThanScattered()
    {
        Assert.True(FuzzyMatcher.TryMatch("doc", "documents", out int prefixScore));
        Assert.True(FuzzyMatcher.TryMatch("doc", "downloads-orange-cat", out int scatteredScore));
        Assert.True(prefixScore > scatteredScore);
    }

    [Fact]
    public void WordBoundaryMatch_ScoresHigherThanMidWord()
    {
        Assert.True(FuzzyMatcher.TryMatch("vm", "view_model", out int boundaryScore));
        Assert.True(FuzzyMatcher.TryMatch("vm", "avmxxxxxxx", out int midScore));
        Assert.True(boundaryScore > midScore);
    }
}
