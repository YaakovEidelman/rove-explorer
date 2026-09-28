using Rove.Core.Services;
using Xunit;

namespace Rove.Core.Tests;

public class GioPromptTests
{
    [Fact]
    public void NoPromptWhileOutputIsStillArriving()
    {
        Assert.Null(GioPrompt.TryRead("Authentication Required\nEnter user and password for “host”:\n"));
        Assert.Null(GioPrompt.TryRead("User"));
    }

    [Fact]
    public void AUserPromptCarriesTheMessageAndSuggestion()
    {
        GioPrompt? prompt = GioPrompt.TryRead("Authentication Required\nEnter user and password for “host”:\nUser [me]: ");

        Assert.NotNull(prompt);
        Assert.Equal("User", prompt.Field);
        Assert.Equal("me", prompt.Suggested);
        Assert.Equal("Authentication Required\nEnter user and password for “host”:", prompt.Message);
        Assert.False(prompt.IsSecret);
    }

    [Fact]
    public void APasswordPromptIsSecret()
    {
        GioPrompt? prompt = GioPrompt.TryRead("Password: ");

        Assert.NotNull(prompt);
        Assert.True(prompt.IsSecret);
        Assert.Null(prompt.Suggested);
        Assert.Equal("", prompt.Message);
    }

    [Fact]
    public void AQuestionListsItsChoices()
    {
        GioPrompt? prompt = GioPrompt.TryRead(
            "Can’t verify the identity of “host”.\n[1] Log In Anyway\n[2] Cancel Login\nChoice: ");

        Assert.NotNull(prompt);
        Assert.True(prompt.IsChoice);
        Assert.Equal(["Log In Anyway", "Cancel Login"], prompt.Choices);
        Assert.Equal("Can’t verify the identity of “host”.", prompt.Message);
    }

    [Fact]
    public void TextThatOnlyEndsInAColonIsNotAPrompt()
    {
        Assert.Null(GioPrompt.TryRead("Enter the Username: "));
    }
}
