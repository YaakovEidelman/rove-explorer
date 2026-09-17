namespace Rove.Core.Protocol;

/// <summary>
/// Uniform result envelope for every backend verb.
/// Status is "success" or "error"; Reason is a stable machine code
/// (e.g. "permission_denied"), Message is optional human-readable detail.
/// </summary>
public record struct CommandResult(string Status, string Reason, string? Message, object? Data)
{
    public static CommandResult Ok(object? data) => new("success", "", null, data);
    public static CommandResult Fail(string reason, string? message) => new("error", reason, message, null);

    public readonly bool IsOk => Status == "success";
}

public record struct CommandResult<T>(string Status, string Reason, string? Message, T? Data)
{
    public static CommandResult<T> Ok(T? data) => new("success", "", null, data);
    public static CommandResult<T> Fail(string reason, string? message) => new("error", reason, message, default);
    public static CommandResult<T> Fail(string reason, string? message, T? data) => new("error", reason, message, data);

    public readonly bool IsOk => Status == "success";

    public static implicit operator CommandResult(CommandResult<T> r) =>
        new(r.Status, r.Reason, r.Message, r.Data);
}

/// <summary>
/// Outcome of one item inside a multi-item operation (move/copy/delete).
/// Multi-item verbs never fail as a unit; they report per item so the UI
/// can tell the user exactly what happened to what.
/// </summary>
public record OpResult(string Path, bool Ok, string Reason, string? Message, FolderItem? Item)
{
    public static OpResult Success(string path, FolderItem? item = null) => new(path, true, "", null, item);
    public static OpResult Failure(string path, string reason, string? message) => new(path, false, reason, message, null);
}
