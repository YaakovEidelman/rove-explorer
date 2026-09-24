using System.Text.Json.Serialization;

namespace Rove.Core.Services;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(ThemeColors))]
internal partial class ThemeColorsJson : JsonSerializerContext
{
}
