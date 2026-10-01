using Rove.Core.Services;

namespace Rove.UI.Services;

public sealed class SavedAnswerPrompter(IMountPrompter inner, string? user, string? password) : IMountPrompter
{
    private string? _user = user;
    private string? _password = password;

    public string? TypedUser { get; private set; }

    public string? TypedPassword { get; private set; }

    public string? UsedPassword { get; private set; }

    public async Task<string?> AskTextAsync(string message, string field, bool secret, string? suggested)
    {
        if (secret && _password is { } known)
        {
            _password = null;
            UsedPassword = known;
            return known;
        }
        if (field == "User" && _user is { Length: > 0 } name)
        {
            _user = null;
            return name;
        }

        string? answer = await inner.AskTextAsync(message, field, secret, suggested).ConfigureAwait(false);
        if (secret)
        {
            TypedPassword = answer;
            UsedPassword = answer;
        }
        else if (field == "User")
            TypedUser = answer;
        return answer;
    }

    public Task<int?> ChooseAsync(string message, string[] choices) => inner.ChooseAsync(message, choices);
}
