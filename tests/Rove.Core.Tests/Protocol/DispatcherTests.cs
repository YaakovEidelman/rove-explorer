using System.Text.Json;
using Rove.Core.Protocol;
using Xunit;

namespace Rove.Core.Tests;

public class DispatcherTests
{
    private readonly Dispatcher _dispatcher = new();

    private static JsonElement Json(object o) =>
        JsonSerializer.SerializeToElement(o);

    [Fact]
    public async Task UnknownCommand_Fails()
    {
        CommandResult result = await _dispatcher.Dispatch(new("1", "no_such_verb", Json(new { })));
        Assert.Equal("error", result.Status);
        Assert.Equal("unknown_command", result.Reason);
    }

    [Fact]
    public async Task BadArguments_FailCleanly()
    {
        CommandResult result = await _dispatcher.Dispatch(new("2", "read_directory", Json(42)));
        Assert.Equal("error", result.Status);
        Assert.Equal("bad_arguments", result.Reason);
    }

    [Fact]
    public async Task ReadDirectory_RoundTripsThroughJson()
    {
        using TempDir tmp = new();
        tmp.File("hello.txt", "hi");

        CommandResult result = await _dispatcher.Dispatch(
            new("3", "read_directory", Json(new { Path = tmp.Path })));

        Assert.Equal("success", result.Status);
        FolderItem[] items = Assert.IsType<FolderItem[]>(result.Data);
        Assert.Single(items);
        Assert.Equal("hello.txt", items[0].Name);
    }

    [Fact]
    public async Task EveryStableVerb_IsRegistered()
    {
        string[] verbs =
        [
            "read_directory", "get_parent", "launch_file", "get_icon",
            "rename_item", "move_items", "copy_items", "delete_items",
            "delete_items_permanent", "create_item", "get_metadata", "search_global",
            "restore_items", "delete_if_empty", "list_open_with", "launch_file_with",
        ];
        foreach (string verb in verbs)
            Assert.Contains(verb, _dispatcher.DispatcherDict.Keys);
        await Task.CompletedTask;
    }

    [Fact]
    public void EveryArgumentTypeTheDispatcherUsesHasAGeneratedReader()
    {
        Type[] argTypes =
        [
            typeof(ReadDirectoryArgs), typeof(GetParentArgs), typeof(ResolvePathArgs),
            typeof(LaunchFileArgs), typeof(ListOpenWithArgs), typeof(LaunchFileWithArgs),
            typeof(ListDrivesArgs), typeof(GetIconArgs),
            typeof(RenameItemArgs), typeof(MoveItemsArgs), typeof(CopyItemsArgs),
            typeof(DeleteItemsArgs), typeof(RestoreItemsArgs), typeof(OpenTrashArgs),
            typeof(ExtractArchivesArgs), typeof(CompressItemsArgs), typeof(DeleteIfEmptyArgs),
            typeof(CreateItemArgs), typeof(GetMetadataArgs), typeof(SearchGlobalArgs),
        ];

        foreach (Type argType in argTypes)
            Assert.NotNull(ProtocolJson.Default.GetTypeInfo(argType));
    }
}
