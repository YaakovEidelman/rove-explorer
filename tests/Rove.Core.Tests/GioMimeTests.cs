using Rove.Core.Services;
using Xunit;

namespace Rove.Core.Tests;

public class GioMimeTests
{
    private const string TextPlain =
        "Default application for “text/plain”: nvim.desktop\n"
        + "Registered applications:\n\tlibreoffice-writer.desktop\n\tnvim.desktop\n\tomawrite.desktop\n"
        + "Recommended applications:\n\tnvim.desktop\n\tomawrite.desktop\n";

    [Fact]
    public void TheDefaultComesFirstThenTheRecommendedThenTheRest()
    {
        Assert.Equal(
            ["nvim.desktop", "omawrite.desktop", "libreoffice-writer.desktop"],
            GioMime.ParseAppIds(TextPlain));
    }

    [Fact]
    public void ATypeWithOnlyInheritedAppsStillListsThem()
    {
        string output =
            "Default application for “text/x-python”: nvim.desktop\n"
            + "Registered applications:\n\tnvim.desktop\n\tomawrite.desktop\n"
            + "No recommended applications\n";

        Assert.Equal(["nvim.desktop", "omawrite.desktop"], GioMime.ParseAppIds(output));
    }

    [Fact]
    public void ATypeNothingOpensListsNothing()
    {
        Assert.Empty(GioMime.ParseAppIds("No default applications for “application/x-zerosize”\n"));
    }

    [Fact]
    public void TheContentTypeIsReadOutOfGioInfo()
    {
        Assert.Equal(
            "text/plain",
            GioMime.ParseContentType("uri: file:///tmp/t.txt\nattributes:\n  standard::content-type: text/plain\n"));
    }

    [Fact]
    public void NoContentTypeLineMeansNoType()
    {
        Assert.Null(GioMime.ParseContentType("gio: file:///tmp/nope: Error when getting information\n"));
    }
}
