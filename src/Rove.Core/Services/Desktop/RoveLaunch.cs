using System.Runtime.Versioning;

namespace Rove.Core.Services;

[SupportedOSPlatform("linux")]
public static class RoveLaunch
{
    public const string AppId = "rove";

    public static string BinDirectory => Path.Combine(XdgPaths.Home, ".local", "bin");

    public static string ExecutablePath()
    {
        string installed = Path.Combine(BinDirectory, AppId);
        return File.Exists(installed) ? installed : AppId;
    }
}
