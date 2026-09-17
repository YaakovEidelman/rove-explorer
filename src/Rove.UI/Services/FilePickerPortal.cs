using Rove.Core.Services;
using System.Runtime.Versioning;

namespace Rove.UI.Services;

[SupportedOSPlatform("linux")]
public sealed class FilePickerPortal
{
    private readonly string _dataHome;
    private readonly string _configPath;
    private readonly string _statePath;
    private readonly string _executable;

    public FilePickerPortal() : this(
        XdgPaths.DataHome,
        PortalInstall.ConfigPath(XdgPaths.ConfigHome),
        RovePaths.PortalInstallStateFile,
        LinuxInstall.PortalBinaryPath(LinuxInstall.LibDirectory))
    {
    }

    public FilePickerPortal(string dataHome, string configPath, string statePath, string executable)
    {
        _dataHome = dataHome;
        _configPath = configPath;
        _statePath = statePath;
        _executable = executable;
    }

    public PortalStatus Status => PortalInstall.CurrentStatus(_configPath, _statePath);

    public bool Enable() =>
        PortalInstall.Enable(_dataHome, _configPath, _statePath, _executable, PortalFiles.PreferredName);

    public bool Disable() => PortalInstall.Disable(_dataHome, _configPath, _statePath);

    public void EnsureRegisteredInBackground()
    {
        PortalInstall.Advertise(_dataHome, _executable);
        PortalInstall.EnsureBackend(_configPath, _statePath, PortalFiles.PreferredName);
    }
}
