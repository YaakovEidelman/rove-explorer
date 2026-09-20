using Rove.Core.Protocol;
using Rove.UI.ViewModels;
using Xunit;

namespace Rove.UI.Tests;

public class ListViewItemTests
{
    private static FolderItem Item(string name, bool isDirectory = false, string extension = "", long? size = null) =>
        new(name, "/test/" + name, FileAttributes.Normal, DateTime.UnixEpoch, isDirectory, size, extension);

    [Fact]
    public void ADirectoryReadsAsAFolder()
    {
        ListViewItem row = new(Item("src", isDirectory: true), 16, new NullIconCache());

        Assert.Equal("Folder", row.TypeText);
    }

    [Fact]
    public void AFileWithAnExtensionReadsAsThatExtensionUppercased()
    {
        ListViewItem row = new(Item("notes.txt", extension: ".txt"), 16, new NullIconCache());

        Assert.Equal("TXT", row.TypeText);
    }

    [Fact]
    public void AFileWithNoExtensionJustReadsAsAFile()
    {
        ListViewItem row = new(Item("README"), 16, new NullIconCache());

        Assert.Equal("File", row.TypeText);
    }

    [Fact]
    public void CuttingARowDimsIt()
    {
        ListViewItem row = new(Item("a.txt"), 16, new NullIconCache());
        Assert.Equal(1.0, row.RowOpacity);

        row.IsCut = true;

        Assert.Equal(0.45, row.RowOpacity);
    }

    [Fact]
    public void UpdatingDataRefreshesTheDerivedText()
    {
        ListViewItem row = new(Item("a.txt", size: 10), 16, new NullIconCache());
        Assert.Equal("10 B", row.SizeText);

        row.UpdateData(Item("a.txt", size: 2048));

        Assert.Equal("2 KB", row.SizeText);
    }
}
