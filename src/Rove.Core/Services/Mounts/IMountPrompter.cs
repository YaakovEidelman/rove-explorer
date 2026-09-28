namespace Rove.Core.Services;

public interface IMountPrompter
{
    Task<string?> AskTextAsync(string message, string field, bool secret, string? suggested);

    Task<int?> ChooseAsync(string message, string[] choices);
}
