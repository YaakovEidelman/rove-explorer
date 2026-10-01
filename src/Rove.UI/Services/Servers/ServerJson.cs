using System.Text.Json.Serialization;

namespace Rove.UI.Services;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(List<SavedServer>))]
internal partial class ServerJson : JsonSerializerContext
{
}
