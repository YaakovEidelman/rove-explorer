namespace Rove.Core.Protocol;

public record ResponseEnvelope(string CommandId, string Command, CommandResult Result);
