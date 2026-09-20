using System.Text.Json.Serialization;

namespace Rove.Core.Protocol;

[JsonSerializable(typeof(ReadDirectoryArgs))]
[JsonSerializable(typeof(GetParentArgs))]
[JsonSerializable(typeof(ResolvePathArgs))]
[JsonSerializable(typeof(LaunchFileArgs))]
[JsonSerializable(typeof(ListOpenWithArgs))]
[JsonSerializable(typeof(LaunchFileWithArgs))]
[JsonSerializable(typeof(GetIconArgs))]
[JsonSerializable(typeof(RenameItemArgs))]
[JsonSerializable(typeof(MoveItemsArgs))]
[JsonSerializable(typeof(CopyItemsArgs))]
[JsonSerializable(typeof(DeleteItemsArgs))]
[JsonSerializable(typeof(ExtractArchivesArgs))]
[JsonSerializable(typeof(CompressItemsArgs))]
[JsonSerializable(typeof(RestoreItemsArgs))]
[JsonSerializable(typeof(OpenTrashArgs))]
[JsonSerializable(typeof(DeleteIfEmptyArgs))]
[JsonSerializable(typeof(CreateItemArgs))]
[JsonSerializable(typeof(ListDrivesArgs))]
[JsonSerializable(typeof(SearchGlobalArgs))]
[JsonSerializable(typeof(GetMetadataArgs))]
[JsonSerializable(typeof(FolderItem))]
internal partial class ProtocolJson : JsonSerializerContext
{
}
