using Rove.Core.Services;
using Xunit;

namespace Rove.Core.Tests;

public class FileManagerBusInstallTests
{
    [Fact]
    public void InstallingWritesAServiceFileNamingRoveAsTheExecutable()
    {
        using FileManagerBusHome home = new();

        FileManagerBusInstall.Install(home.DataHome, "/opt/rove/rove-portal");

        string serviceFile = FileManagerBusInstall.ServiceFilePath(home.DataHome);
        Assert.True(File.Exists(serviceFile));
        string contents = File.ReadAllText(serviceFile);
        Assert.Contains($"Name={FileManagerBusFiles.BusName}", contents, StringComparison.Ordinal);
        Assert.Contains("Exec=/opt/rove/rove-portal", contents, StringComparison.Ordinal);
    }

    [Fact]
    public void WithdrawingRemovesTheServiceFile()
    {
        using FileManagerBusHome home = new();
        FileManagerBusInstall.Install(home.DataHome, "/opt/rove/rove-portal");

        FileManagerBusInstall.Withdraw(home.DataHome);

        Assert.False(File.Exists(FileManagerBusInstall.ServiceFilePath(home.DataHome)));
    }

    [Fact]
    public void WithdrawingWithNothingInstalledDoesNothing()
    {
        using FileManagerBusHome home = new();

        FileManagerBusInstall.Withdraw(home.DataHome);

        Assert.False(File.Exists(FileManagerBusInstall.ServiceFilePath(home.DataHome)));
    }
}
