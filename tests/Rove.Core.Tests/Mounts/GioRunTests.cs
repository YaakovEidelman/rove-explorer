using Rove.Core.Services;
using Xunit;

namespace Rove.Core.Tests;

public class GioRunTests
{
    [Fact]
    public void TheLocationPrefixIsDroppedFromErrors()
    {
        GioRun run = new(2, "", "gio: ftp://ftp.gnu.org/: The specified location is not mounted\n", Prompted: false);

        Assert.Equal("The specified location is not mounted", run.Error("fallback"));
    }

    [Fact]
    public void NoErrorTextFallsBack()
    {
        GioRun run = new(2, "", "", Prompted: false);

        Assert.Equal("fallback", run.Error("fallback"));
    }
}
