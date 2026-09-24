using System.Text.Json;

namespace Rove.Core.Protocol;

public record RequestEnvelope(string CommandId, string Command, JsonElement Args);
