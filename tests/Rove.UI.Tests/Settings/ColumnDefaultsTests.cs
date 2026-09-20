using System.Collections.ObjectModel;
using Rove.UI.Models;
using Rove.UI.Services;
using Xunit;

namespace Rove.UI.Tests;

public class ColumnDefaultsTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData(0L, "0 B")]
    [InlineData(500L, "500 B")]
    [InlineData(1024L, "1 KB")]
    [InlineData(1536L, "1.5 KB")]
    [InlineData(1024L * 1024, "1 MB")]
    [InlineData(1024L * 1024 * 1024, "1 GB")]
    public void FormatsSizesLikeAFileManagerDoes(long? bytes, string expected)
    {
        Assert.Equal(expected, ColumnDefaults.FormatSize(bytes));
    }

    [Fact]
    public void TheDefaultColumnsAreNameTypeSizeAndModified_AllVisible()
    {
        ObservableCollection<FolderViewColumn> columns = ColumnDefaults.Create();

        Assert.Equal(["Name", "Type", "Size", "Modified"], columns.Select(c => c.Name));
        Assert.All(columns, c => Assert.True(c.IsVisible));
    }
}
