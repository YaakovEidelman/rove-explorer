using Rove.Core.Services;
using System.Runtime.Versioning;

namespace Rove.UI.Services;

[SupportedOSPlatform("linux")]
public sealed class FilePickerPortal
{
    private readonly string _dataHome;
    private readonly string _configPath;
    private readonly string _statePath;
    private readonly string _askedPath;
    private readonly string _mimeAppsPath;
    private readonly string _mimeStatePath;
    private readonly string _executable;
    private readonly string _desktopFileName;
    private readonly Action? _restartDesktopPortal;

    public FilePickerPortal() : this(
        XdgPaths.DataHome,
        PortalInstall.ConfigPath(XdgPaths.ConfigHome, XdgPaths.CurrentDesktop),
        RovePaths.PortalInstallStateFile,
        RovePaths.PortalAskedFile,
        DefaultFileManager.MimeAppsPath(XdgPaths.ConfigHome),
        RovePaths.MimeDefaultStateFile,
        LinuxInstall.PortalBinaryPath(LinuxInstall.LibDirectory),
        LinuxInstall.AppId + ".desktop",
        PortalInstall.RestartDesktopPortal)
    {
    }

    public FilePickerPortal(
        string dataHome, string configPath, string statePath, string askedPath,
        string mimeAppsPath, string mimeStatePath, string executable, string desktopFileName,
        Action? restartDesktopPortal = null)
    {
        _dataHome = dataHome;
        _configPath = configPath;
        _statePath = statePath;
        _askedPath = askedPath;
        _mimeAppsPath = mimeAppsPath;
        _mimeStatePath = mimeStatePath;
        _executable = executable;
        _desktopFileName = desktopFileName;
        _restartDesktopPortal = restartDesktopPortal;
    }

    /// <summary>
    /// The Open/Save dialog claim is the one shown — Enable/Disable always
    /// move the default-folder-handler claim in lockstep with it, so the two
    /// never disagree in practice.
    /// </summary>
    public PortalStatus Status => PortalInstall.CurrentStatus(_configPath, _statePath);

    public bool HasAskedAboutDefault => PortalInstall.HasAskedAboutDefault(_askedPath);

    public void MarkAskedAboutDefault() => PortalInstall.MarkAskedAboutDefault(_askedPath);

    public bool Enable()
    {
        bool portal = PortalInstall.Enable(
            _dataHome, _configPath, _statePath, _executable, PortalFiles.PreferredName, _restartDesktopPortal);
        bool mime = DefaultFileManager.Enable(_mimeAppsPath, _mimeStatePath, _desktopFileName);
        if (mime)
            FileManagerBusInstall.Install(_dataHome, _executable);
        return portal || mime;
    }

    public bool Disable()
    {
        bool portal = PortalInstall.Disable(_dataHome, _configPath, _statePath, _executable, _restartDesktopPortal);
        bool mime = DefaultFileManager.Disable(_mimeAppsPath, _mimeStatePath);
        if (mime)
            FileManagerBusInstall.Withdraw(_dataHome);
        return portal || mime;
    }

    public void AdvertiseInBackground()
    {
        PortalInstall.Advertise(_dataHome, _executable);
        if (DefaultFileManager.CurrentStatus(_mimeAppsPath, _mimeStatePath) == PortalStatus.OwnedByRove)
            FileManagerBusInstall.Install(_dataHome, _executable);
    }
}
