namespace Rove.Core.Protocol;

public record struct CommandResult(string Status, string Reason, string? Message, object? Data)
{
    public static CommandResult Ok(object? data) => new("success", "", null, data);
    public static CommandResult Fail(string reason, string? message) => new("error", reason, message, null);

    public readonly bool IsOk => Status == "success";
}
