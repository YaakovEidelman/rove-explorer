using Rove.UI.Services;
using Xunit;

namespace Rove.UI.Tests;

public class PickerLaunchOptionsTests
{
    [Fact]
    public void WithoutThePickerFlagThereIsNoPickerLaunch()
    {
        Assert.Null(PickerLaunchOptions.Parse(["--start-dir", "/tmp", "--out", "/tmp/out.txt"]));
    }

    [Fact]
    public void MissingStartDirOrOutFailsTheWholeParse()
    {
        Assert.Null(PickerLaunchOptions.Parse(["--picker", "--out", "/tmp/out.txt"]));
        Assert.Null(PickerLaunchOptions.Parse(["--picker", "--start-dir", "/tmp"]));
    }

    [Fact]
    public void BareFlagsDefaultOff()
    {
        PickerLaunchOptions? options =
            PickerLaunchOptions.Parse(["--picker", "--start-dir", "/tmp", "--out", "/tmp/out.txt"]);

        Assert.NotNull(options);
        Assert.False(options.Multiple);
        Assert.False(options.Directory);
        Assert.Empty(options.Filters);
        Assert.Equal(0, options.SelectedFilterIndex);
        Assert.Null(options.ParentWindow);
    }

    [Fact]
    public void ParentWindowIsCapturedWhenPresent()
    {
        PickerLaunchOptions? options = PickerLaunchOptions.Parse(
            ["--picker", "--start-dir", "/tmp", "--out", "/tmp/out.txt", "--parent-window", "x11:1a2b3c"]);

        Assert.NotNull(options);
        Assert.Equal("x11:1a2b3c", options.ParentWindow);
    }

    [Fact]
    public void MultipleAndDirectoryAreIndependentFlags()
    {
        PickerLaunchOptions? options = PickerLaunchOptions.Parse(
            ["--picker", "--start-dir", "/tmp", "--out", "/tmp/out.txt", "--multiple", "--directory"]);

        Assert.NotNull(options);
        Assert.True(options.Multiple);
        Assert.True(options.Directory);
    }

    [Fact]
    public void RepeatedFilterPatternFlagsAllCollectUnderOneFilterGroup()
    {
        PickerLaunchOptions? options = PickerLaunchOptions.Parse(
        [
            "--picker", "--start-dir", "/tmp", "--out", "/tmp/out.txt",
            "--filter-name", "Images",
            "--filter-pattern", "*.png",
            "--filter-pattern", "*.jpg",
        ]);

        Assert.NotNull(options);
        PickerFilter filter = Assert.Single(options.Filters);
        Assert.Equal("Images", filter.Name);
        Assert.Equal(["*.png", "*.jpg"], filter.Patterns);
        Assert.Equal(0, options.SelectedFilterIndex);
    }

    [Fact]
    public void MultipleFilterNamesEachCollectTheirOwnPatterns()
    {
        PickerLaunchOptions? options = PickerLaunchOptions.Parse(
        [
            "--picker", "--start-dir", "/tmp", "--out", "/tmp/out.txt",
            "--filter-name", "Images",
            "--filter-pattern", "*.png",
            "--filter-name", "Text",
            "--filter-pattern", "*.txt",
            "--filter-pattern", "*.md",
            "--filter-selected", "1",
        ]);

        Assert.NotNull(options);
        Assert.Equal(2, options.Filters.Length);
        Assert.Equal("Images", options.Filters[0].Name);
        Assert.Equal(["*.png"], options.Filters[0].Patterns);
        Assert.Equal("Text", options.Filters[1].Name);
        Assert.Equal(["*.txt", "*.md"], options.Filters[1].Patterns);
        Assert.Equal(1, options.SelectedFilterIndex);
    }

    [Fact]
    public void OutOfRangeSelectedFilterIndexClampsToLastFilter()
    {
        PickerLaunchOptions? options = PickerLaunchOptions.Parse(
        [
            "--picker", "--start-dir", "/tmp", "--out", "/tmp/out.txt",
            "--filter-name", "Images",
            "--filter-pattern", "*.png",
            "--filter-selected", "99",
        ]);

        Assert.NotNull(options);
        Assert.Equal(0, options.SelectedFilterIndex);
    }
}
