using System.Text.Json.Serialization;

namespace Rove.Core.Protocol;

/// <summary>
/// The shapes the protocol can read, worked out when Rove is compiled rather
/// than when it runs. Reflecting over a type to find its properties needs the
/// runtime to write code on the spot, which a natively compiled build cannot
/// do — so every argument record the dispatcher accepts is listed here, and
/// the reader for it is generated in advance.
///
/// <para>A verb whose arguments are not on this list cannot be dispatched.</para>
/// </summary>
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
