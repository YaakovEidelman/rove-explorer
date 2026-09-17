using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Rove.Core.Endpoints;
using Rove.Core.Services;

namespace Rove.Core.Protocol;

public record RequestEnvelope(string CommandId, string Command, JsonElement Args);
public record ResponseEnvelope(string CommandId, string Command, CommandResult Result);

/// <summary>
/// Verb-name → handler router for a JSON transport. The Avalonia UI calls
/// Actions directly; this stays as the protocol boundary so the backend can
/// be lifted out of process without touching handlers.
/// </summary>
public class Dispatcher
{
    private readonly Actions _actions;
    private readonly GlobalSearchService _search;

    public Dictionary<string, Func<JsonElement, Task<CommandResult>>> DispatcherDict { get; } = [];

    public Dispatcher() : this(new Actions(), new GlobalSearchService()) { }

    public Dispatcher(Actions actions, GlobalSearchService search)
    {
        _actions = actions;
        _search = search;

        Register<ReadDirectoryArgs, FolderItem[]>("read_directory", _actions.ReadDirectory);
        Register<GetParentArgs, FolderItem?>("get_parent", _actions.GetParent);
        Register<ResolvePathArgs, FolderItem?>("resolve_path", _actions.ResolvePath);
        Register<LaunchFileArgs, string?>("launch_file", _actions.LaunchFile);
        Register<ListDrivesArgs, DriveEntry[]>("list_drives", _actions.ListDrives);
        Register<GetIconArgs, byte[]?>("get_icon", _actions.GetItemIcon);
        Register<RenameItemArgs, FolderItem?>("rename_item", _actions.RenameItem);
        Register<MoveItemsArgs, OpResult[]>("move_items", _actions.MoveItems);
        Register<CopyItemsArgs, OpResult[]>("copy_items", _actions.CopyItems);
        Register<DeleteItemsArgs, OpResult[]>("delete_items", _actions.DeleteItems);
        Register<DeleteItemsArgs, OpResult[]>("delete_items_permanent", _actions.DeleteItemsPermanent);
        Register<RestoreItemsArgs, OpResult[]>("restore_items", _actions.RestoreItems);
        Register<RestoreItemsArgs, OpResult[]>("restore_trashed_items", _actions.RestoreTrashedItems);
        Register<OpenTrashArgs, string?>("open_trash", _actions.OpenTrash);
        Register<ExtractArchivesArgs, OpResult[]>("extract_archives", _actions.ExtractArchives);
        Register<CompressItemsArgs, OpResult[]>("compress_items", _actions.CompressItems);
        Register<DeleteIfEmptyArgs, string?>("delete_if_empty", _actions.DeleteIfEmpty);
        Register<CreateItemArgs, FolderItem?>("create_item", _actions.CreateItem);
        Register<GetMetadataArgs, ItemMetadata?>("get_metadata", a => _actions.GetMetadataAsync(a));
        Register<SearchGlobalArgs, SearchHit[]>("search_global",
            a => _search.SearchAsync(a.Root, a.Query, a.MaxResults, CancellationToken.None));
    }

    public async Task<CommandResult> Dispatch(RequestEnvelope envelope)
    {
        try
        {
            if (!DispatcherDict.TryGetValue(envelope.Command, out Func<JsonElement, Task<CommandResult>>? method))
                return CommandResult.Fail("unknown_command", envelope.Command);
            return await method(envelope.Args);
        }
        catch (Exception ex)
        {
            return CommandResult.Fail("unhandled", ex.Message);
        }
    }

    private void Register<TArgs, TData>(string command, Func<TArgs, CommandResult<TData>> handler) =>
        Register<TArgs, TData>(command, args => Task.FromResult(handler(args)));

    private void Register<TArgs, TData>(string command, Func<TArgs, Task<CommandResult<TData>>> handler)
    {
        DispatcherDict[command] = async (JsonElement json) =>
        {
            // The reader for these arguments is generated at compile time; a
            // type nobody listed in ProtocolJson has none, and cannot be read.
            if (ProtocolJson.Default.GetTypeInfo(typeof(TArgs)) is not JsonTypeInfo<TArgs> shape)
                return CommandResult.Fail("bad_arguments", $"{typeof(TArgs).Name} is not a known argument shape.");

            TArgs? typedArgs;
            try
            {
                typedArgs = JsonSerializer.Deserialize(json, shape);
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException)
            {
                return CommandResult.Fail("bad_arguments", ex.Message);
            }
            if (typedArgs is null)
                return CommandResult.Fail("bad_arguments", null);
            return await handler(typedArgs);
        };
    }
}
