using Rove.Core.Services;

namespace Rove.Core.Tests;

public sealed class PortalHome : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "rove-portal-" + Guid.NewGuid().ToString("N"));

    public string DataHome => Path.Combine(Root, "share");

    public string ConfigHome => Path.Combine(Root, "config");

    public string StatePath => Path.Combine(Root, "state", "portal-install.json");

    public string AskedPath => Path.Combine(Root, "state", "portal-asked.json");

    public string ConfigPath => PortalInstall.ConfigPath(ConfigHome);

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch
        {
        }
    }
}
