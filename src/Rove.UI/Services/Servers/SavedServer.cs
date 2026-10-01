using System.Text.Json.Serialization;

namespace Rove.UI.Services;

public sealed record SavedServer(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("address")] string Address,
    [property: JsonPropertyName("savePassword")] bool SavePassword
);
