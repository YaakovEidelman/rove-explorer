using Rove.UI.Models;
using Xunit;

namespace Rove.UI.Tests;

public class DateGroupLabelerTests
{
    private static readonly DateTime _now = new(2026, 9, 23, 15, 0, 0);

    [Theory]
    [InlineData(0, "Today")]
    [InlineData(1, "Yesterday")]
    public void RecentDaysGetTheirOwnLabel(int daysAgo, string expected) =>
        Assert.Equal(expected, DateGroupLabeler.LabelFor(_now.AddDays(-daysAgo), _now));

    [Fact]
    public void EarlierThisWeekCoversTheRestOfTheCurrentWeek() =>
        Assert.Equal("Earlier This Week", DateGroupLabeler.LabelFor(new DateTime(2026, 9, 21), _now));

    [Fact]
    public void LastWeekIsTheSevenDaysBeforeThisWeek() =>
        Assert.Equal("Last Week", DateGroupLabeler.LabelFor(new DateTime(2026, 9, 14), _now));

    [Fact]
    public void EarlierThisMonthCoversTheRestOfTheMonth() =>
        Assert.Equal("Earlier This Month", DateGroupLabeler.LabelFor(new DateTime(2026, 9, 3), _now));

    [Fact]
    public void LastMonthIsTheCalendarMonthBefore() =>
        Assert.Equal("Last Month", DateGroupLabeler.LabelFor(new DateTime(2026, 8, 15), _now));

    [Fact]
    public void EarlierThisYearCoversTheRestOfTheYear() =>
        Assert.Equal("Earlier This Year", DateGroupLabeler.LabelFor(new DateTime(2026, 3, 1), _now));

    [Fact]
    public void LastYearGetsItsOwnLabel() =>
        Assert.Equal("Last Year", DateGroupLabeler.LabelFor(new DateTime(2025, 5, 1), _now));

    [Fact]
    public void OlderYearsAreLabeledByYear() =>
        Assert.Equal("2019", DateGroupLabeler.LabelFor(new DateTime(2019, 1, 1), _now));

    [Fact]
    public void AFutureTimestampCountsAsToday() =>
        Assert.Equal("Today", DateGroupLabeler.LabelFor(_now.AddHours(2), _now));
}
