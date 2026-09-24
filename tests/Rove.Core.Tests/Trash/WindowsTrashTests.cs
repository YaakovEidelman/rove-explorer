using System.Buffers.Binary;
using System.Text;
using Rove.Core.Services;
using Xunit;

namespace Rove.Core.Tests;

public class WindowsTrashTests
{
    private const string UserFolder = "S-1-5-21-1111111111-222222222-3333333333-1001";

    private static void Trash(string binRoot, string tag, string originalPath, string content, bool legacy = false)
    {
        string user = Path.Combine(binRoot, UserFolder);
        Directory.CreateDirectory(user);
        File.WriteAllText(Path.Combine(user, "$R" + tag), content);
        File.WriteAllBytes(Path.Combine(user, "$I" + tag), Record(originalPath, content.Length, legacy));
    }

    private static void TrashFolder(string binRoot, string tag, string originalPath)
    {
        string user = Path.Combine(binRoot, UserFolder);
        Directory.CreateDirectory(Path.Combine(user, "$R" + tag));
        File.WriteAllText(Path.Combine(user, "$R" + tag, "inside.txt"), "content");
        File.WriteAllBytes(Path.Combine(user, "$I" + tag), Record(originalPath, 0, legacy: false));
    }

    private static byte[] Record(string originalPath, long size, bool legacy)
    {
        byte[] path = Encoding.Unicode.GetBytes(originalPath + '\0');
        byte[] bytes = legacy
            ? new byte[24 + (260 * 2)]
            : new byte[24 + 4 + path.Length];

        BinaryPrimitives.WriteInt64LittleEndian(bytes, legacy ? 1 : 2);
        BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(8), size);
        BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(16), DateTime.UtcNow.ToFileTimeUtc());

        if (legacy)
        {
            path.CopyTo(bytes.AsSpan(24));
        }
        else
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(24), path.Length / 2);
            path.CopyTo(bytes.AsSpan(28));
        }
        return bytes;
    }

    private static string[] BinRoots(TempDir tmp) => [tmp.Sub("$Recycle.Bin")];

    [Fact]
    public void PuttingSomethingBackReturnsItToWhereItWasDeletedFrom()
    {
        using TempDir tmp = new();
        string original = tmp.Sub("notes.txt");
        Trash(tmp.Sub("$Recycle.Bin"), "ABC123.txt", original, "keep me");

        string[] restored = WindowsTrash.Restore([original], BinRoots(tmp));

        Assert.Equal([original], restored);
        Assert.Equal("keep me", File.ReadAllText(original));
        Assert.False(File.Exists(tmp.Sub("$Recycle.Bin", UserFolder, "$IABC123.txt")));
        Assert.False(File.Exists(tmp.Sub("$Recycle.Bin", UserFolder, "$RABC123.txt")));
    }

    [Fact]
    public void ARecordFromOlderWindowsIsReadTheSameWay()
    {
        using TempDir tmp = new();
        string original = tmp.Sub("older.txt");
        Trash(tmp.Sub("$Recycle.Bin"), "OLD1.txt", original, "from vista", legacy: true);

        string[] restored = WindowsTrash.Restore([original], BinRoots(tmp));

        Assert.Equal([original], restored);
        Assert.Equal("from vista", File.ReadAllText(original));
    }

    [Fact]
    public void AFolderComesBackWithEverythingInIt()
    {
        using TempDir tmp = new();
        string original = tmp.Sub("project");
        TrashFolder(tmp.Sub("$Recycle.Bin"), "DIR1", original);

        string[] restored = WindowsTrash.Restore([original], BinRoots(tmp));

        Assert.Equal([original], restored);
        Assert.Equal("content", File.ReadAllText(Path.Combine(original, "inside.txt")));
    }

    [Fact]
    public void SomethingThatTookTheNameBackIsNotWrittenOver()
    {
        using TempDir tmp = new();
        string original = tmp.File("notes.txt", "a newer file by the same name");
        Trash(tmp.Sub("$Recycle.Bin"), "ABC124.txt", original, "the trashed one");

        string[] restored = WindowsTrash.Restore([original], BinRoots(tmp));

        Assert.Empty(restored);
        Assert.Equal("a newer file by the same name", File.ReadAllText(original));
        Assert.True(File.Exists(tmp.Sub("$Recycle.Bin", UserFolder, "$RABC124.txt")));
    }

    [Fact]
    public void ARecordWithNoItemLeftRestoresNothing()
    {
        using TempDir tmp = new();
        string original = tmp.Sub("gone.txt");
        Trash(tmp.Sub("$Recycle.Bin"), "ABC125.txt", original, "content");
        File.Delete(tmp.Sub("$Recycle.Bin", UserFolder, "$RABC125.txt"));

        Assert.Empty(WindowsTrash.Restore([original], BinRoots(tmp)));
    }

    [Fact]
    public void SomethingNeverDeletedIsSimplyNotFound()
    {
        using TempDir tmp = new();
        Trash(tmp.Sub("$Recycle.Bin"), "ABC126.txt", tmp.Sub("other.txt"), "content");

        Assert.Empty(WindowsTrash.Restore([tmp.Sub("imaginary.txt")], BinRoots(tmp)));
    }

    [Fact]
    public void RubbishInTheBinIsSteppedOver()
    {
        using TempDir tmp = new();
        string user = Path.Combine(tmp.Sub("$Recycle.Bin"), UserFolder);
        Directory.CreateDirectory(user);
        File.WriteAllBytes(Path.Combine(user, "$Itruncated.txt"), [1, 2, 3]);
        File.WriteAllText(Path.Combine(user, "desktop.ini"), "[.ShellClassInfo]");

        string original = tmp.Sub("notes.txt");
        Trash(tmp.Sub("$Recycle.Bin"), "ABC127.txt", original, "keep me");

        Assert.Equal([original], WindowsTrash.Restore([original], BinRoots(tmp)));
    }

    [Fact]
    public void AnotherAccountsCornerOfTheBinIsNotAProblem()
    {
        using TempDir tmp = new();
        string original = tmp.Sub("notes.txt");
        Directory.CreateDirectory(Path.Combine(tmp.Sub("$Recycle.Bin"), "S-1-5-21-someone-else"));
        Trash(tmp.Sub("$Recycle.Bin"), "ABC128.txt", original, "keep me");

        Assert.Equal([original], WindowsTrash.Restore([original], BinRoots(tmp)));
    }

    [Fact]
    public void SeveralItemsComeBackTogether()
    {
        using TempDir tmp = new();
        string first = tmp.Sub("first.txt");
        string second = tmp.Sub("nested", "second.txt");
        Trash(tmp.Sub("$Recycle.Bin"), "AAA1.txt", first, "one");
        Trash(tmp.Sub("$Recycle.Bin"), "BBB2.txt", second, "two");

        string[] restored = WindowsTrash.Restore([first, second], BinRoots(tmp));

        Assert.Equal(2, restored.Length);
        Assert.Equal("one", File.ReadAllText(first));
        Assert.Equal("two", File.ReadAllText(second));
    }
}
