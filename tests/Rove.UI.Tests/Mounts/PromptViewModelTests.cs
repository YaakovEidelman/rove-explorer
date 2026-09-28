using Rove.UI.Services;
using Rove.UI.ViewModels;
using Xunit;

namespace Rove.UI.Tests;

public class PromptViewModelTests
{
    [Fact]
    public async Task SubmittingGivesBackTheText()
    {
        PromptViewModel prompt = new(new CommandRegistry());
        Task<string?> answer = prompt.AskAsync("Enter password", "Password", secret: true);

        prompt.Text = "hunter2";
        prompt.Apply();

        Assert.Equal("hunter2", await answer);
        Assert.False(prompt.IsOpen);
        Assert.Equal("", prompt.Text);
    }

    [Fact]
    public async Task CancellingGivesBackNothing()
    {
        PromptViewModel prompt = new(new CommandRegistry());
        Task<string?> answer = prompt.AskAsync("Address", "Address", secret: false, "sftp://");

        Assert.Equal("sftp://", prompt.Text);
        prompt.Cancel();

        Assert.Null(await answer);
    }

    [Fact]
    public async Task ASecondQuestionCancelsTheFirst()
    {
        PromptViewModel prompt = new(new CommandRegistry());
        Task<string?> first = prompt.AskAsync("one", "User", secret: false);

        _ = prompt.AskAsync("two", "Password", secret: true);

        Assert.Null(await first);
        Assert.True(prompt.IsOpen);
        Assert.True(prompt.FocusSecret);
        Assert.False(prompt.FocusPlain);
    }

    [Fact]
    public void ConfirmCallsBackOnCancelButNotOnAccept()
    {
        ConfirmViewModel confirm = new(new CommandRegistry());
        List<string> seen = [];

        confirm.Request("trust?", () => seen.Add("yes"), () => seen.Add("no"));
        confirm.Accept();
        confirm.Request("again?", () => seen.Add("yes"), () => seen.Add("no"));
        confirm.Cancel();

        Assert.Equal(["yes", "no"], seen);
    }
}
