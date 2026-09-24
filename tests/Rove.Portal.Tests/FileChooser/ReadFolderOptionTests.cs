using System.Text;
using Tmds.DBus.Protocol;
using Xunit;

namespace Rove.Portal.Tests;

public class ReadFolderOptionTests
{
    [Fact]
    public void WithNoCurrentFolderThereIsNothingToRead()
    {
        Assert.Null(FileChooserHandler.ReadFolderOption([]));
    }

    [Fact]
    public void ACurrentFolderOptionDecodesToItsPath()
    {
        Dictionary<string, VariantValue> options = new()
        {
            ["current_folder"] = VariantValue.Array(Encoding.UTF8.GetBytes("/home/user/Pictures\0")),
        };

        Assert.Equal("/home/user/Pictures", FileChooserHandler.ReadFolderOption(options));
    }
}
