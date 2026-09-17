using Rove.Core.Endpoints;
using Rove.Core.Protocol;
using Xunit;

namespace Rove.Core.Tests;

public class IconFetchTests
{
    private static FolderItem File(string extension) =>
        new("a" + extension, @"C:\x\a" + extension, FileAttributes.Normal, DateTime.Now, false, 1, extension);

    private static FolderItem Folder() =>
        new("folder", @"C:\x\folder", FileAttributes.Directory, DateTime.Now, true, null, "");

    /// <summary>
    /// The shape of a first listing: one request per file type in the folder,
    /// all at once. The Windows shell used to answer only whichever of them
    /// arrived first and turn the rest away empty-handed, which is what left
    /// every file row without an icon until the folder was left and re-entered.
    /// </summary>
    [Fact]
    public async Task AWholeFoldersIconsAskedForAtOnceAllComeBack()
    {
        if (!OperatingSystem.IsWindows())
            return; // this is a Windows shell problem

        Actions actions = new();
        FolderItem[] items =
        [
            Folder(),
            File(".txt"), File(".pdf"), File(".json"), File(".exe"), File(".png"),
            File(".docx"), File(".zip"), File(".cs"), File(".unheard-of"), File(""),
        ];

        CommandResult<byte[]?>[] results = await Task.WhenAll(
            items.Select(item => Task.Run(() => actions.GetItemIcon(new GetIconArgs(item, 16)))));

        for (int i = 0; i < items.Length; i++)
        {
            Assert.True(results[i].IsOk, items[i].Name);
            Assert.NotNull(results[i].Data);
            Assert.NotEmpty(results[i].Data!);
        }
    }

    [Fact]
    public async Task TheSameFileTypeAskedForTwiceIsOnlyFetchedOnce()
    {
        if (!OperatingSystem.IsWindows())
            return;

        Actions actions = new();
        GetIconArgs args = new(File(".txt"), 16);

        byte[]? first = (await actions.GetItemIcon(args)).Data;
        byte[]? second = (await actions.GetItemIcon(args)).Data;

        Assert.NotNull(first);
        Assert.Same(first, second);
    }
}
