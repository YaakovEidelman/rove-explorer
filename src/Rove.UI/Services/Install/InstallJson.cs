using System.Text.Json.Serialization;

namespace Rove.UI.Services;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(InstallRecord))]
internal partial class InstallJson : JsonSerializerContext
{
}
