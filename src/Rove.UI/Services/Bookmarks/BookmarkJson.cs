using System.Text.Json.Serialization;

namespace Rove.UI.Services;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(List<Bookmark>))]
internal partial class BookmarkJson : JsonSerializerContext
{
}
