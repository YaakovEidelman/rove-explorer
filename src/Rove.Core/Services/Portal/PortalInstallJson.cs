using System.Text.Json.Serialization;

namespace Rove.Core.Services;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(PortalInstallState))]
internal partial class PortalInstallJson : JsonSerializerContext
{
}
