using System.Text.Json.Serialization;

namespace Rove.Core.Services;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(DefaultFileManagerState))]
internal partial class DefaultFileManagerJson : JsonSerializerContext
{
}
