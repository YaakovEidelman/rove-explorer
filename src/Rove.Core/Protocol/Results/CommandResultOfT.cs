namespace Rove.Core.Protocol;

public record struct CommandResult<T>(string Status, string Reason, string? Message, T? Data)
{
    public static CommandResult<T> Ok(T? data) => new("success", "", null, data);
    public static CommandResult<T> Fail(string reason, string? message) => new("error", reason, message, default);
    public static CommandResult<T> Fail(string reason, string? message, T? data) => new("error", reason, message, data);

    public readonly bool IsOk => Status == "success";

    public static implicit operator CommandResult(CommandResult<T> r) =>
        new(r.Status, r.Reason, r.Message, r.Data);
}
