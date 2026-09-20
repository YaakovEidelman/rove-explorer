using Rove.Core.Services;
using Xunit;

namespace Rove.Core.Tests;

public class PortalFilesTests
{
    [Fact]
    public void ThePreferredNameIsThePortalFileWithoutItsExtension()
    {
        Assert.Equal("org.freedesktop.impl.portal.Rove", PortalFiles.PreferredName);
    }
}
