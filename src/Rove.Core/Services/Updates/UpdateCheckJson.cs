using System.Text.Json.Serialization;

namespace Rove.Core.Services;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(UpdateCheckState))]
internal partial class UpdateCheckJson : JsonSerializerContext
{
}
