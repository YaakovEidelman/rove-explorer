using Rove.Core.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Rove.UI.Services;

public static class DesktopInstall
{
    public const string OptOutVariable = "ROVE_NO_INSTALL";

    public static void EnsureInBackground()
    {
        if (!IsEligible())
            return;
        _ = Task.Run(() =>
        {
            try
            {
                Run(force: false);
                if (OperatingSystem.IsLinux())
                    new FilePickerPortal().AdvertiseInBackground();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
            {
            }
        });
    }

    public static string InstallNow()
    {
        if (Environment.ProcessPath is null)
            return "Rove could not work out where it is running from.";
        InstallStep step = Run(force: true);
        return step == InstallStep.Nothing
            ? $"Rove is already installed at {InstalledBinary()}."
            : $"Rove is installed at {InstalledBinary()}.";
    }

    public static void Uninstall(Action<string> say)
    {
        string installed = InstalledBinary();
        if (installed.Length == 0)
        {
            say("There is nothing to uninstall on this system.");
            return;
        }

        bool self = Environment.ProcessPath is { Length: > 0 } running
            && PathCompare.PathMatches(running, installed);

        if (OperatingSystem.IsLinux())
        {
            LinuxInstall.UninstallSupport(LinuxInstall.DataHome, LinuxInstall.BinDirectory);
            new FilePickerPortal().Disable();
        }
        else if (OperatingSystem.IsWindows())
        {
            WindowsInstall.UninstallSupport(WindowsInstall.StartMenuDirectory, unregister: true);
        }

        InstallRecord.Delete(RovePaths.InstallRecordFile);
        say(self
            ? "Rove is uninstalled."
            : $"Rove is uninstalled. The copy you ran this from is still at {Environment.ProcessPath}.");

        if (self)
            SelfDelete.After(Path.GetDirectoryName(installed) ?? string.Empty);
        else
            RemoveInstalled();
    }

    public static void DevUninstall(Action<string> say)
    {
        Uninstall(say);
        TryDeleteTree(RovePaths.ConfigDirectory);
        TryDeleteTree(RovePaths.StateDirectory);
        say("Dev uninstall: config and state directories removed too.");
    }

    private static void TryDeleteTree(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static void RemoveInstalled()
    {
        if (OperatingSystem.IsLinux())
            LinuxInstall.RemoveInstalled(LinuxInstall.LibDirectory, LinuxInstall.BinDirectory);
        else if (OperatingSystem.IsWindows())
            WindowsInstall.RemoveInstalled(WindowsInstall.ProgramDirectory);
    }

    internal static InstallStep Decide(
        BuildIdentity running, InstallRecord? installed, bool binaryPresent, bool supportPresent
    )
    {
        if (installed is null || !binaryPresent)
            return InstallStep.Install;
        if (running.IsSameBuildAs(installed.Identity))
            return supportPresent ? InstallStep.Nothing : InstallStep.Repair;
        if (running.IsNewerThan(installed.Identity))
            return InstallStep.Update;
        return InstallStep.KeepInstalled;
    }

    private static InstallStep Run(bool force)
    {
        if (Environment.ProcessPath is not { Length: > 0 } executable)
            return InstallStep.Nothing;
        if (OperatingSystem.IsLinux() && LinuxInstall.OwnedBySystem(LinuxInstall.SystemDataDirs))
            return InstallStep.Nothing;
        if (BuildIdentity.Of(executable, RunningVersion()) is not { } running)
            return InstallStep.Nothing;

        string recordPath = RovePaths.InstallRecordFile;
        InstallRecord? installed = InstallRecord.Read(recordPath);
        string target = InstalledBinary();
        string[] support = SupportFiles();

        InstallStep step = force
            ? InstallStep.Update
            : Decide(running, installed, File.Exists(target), support.All(File.Exists));

        if (step is InstallStep.Nothing or InstallStep.KeepInstalled)
            return step;

        string binary = step == InstallStep.Repair && installed is not null
            ? installed.Binary
            : InstallPayload(executable) ?? executable;

        InstallSupport(binary, running.Version.ToString());

        BuildIdentity recorded = step == InstallStep.Repair && installed is not null
            ? installed.Identity
            : BuildIdentity.Of(binary, running.Version) ?? running;
        InstallRecord.For(binary, recorded, support).Write(recordPath);
        return step;
    }

    private static string? InstallPayload(string executable)
    {
        if (OperatingSystem.IsLinux())
            return LinuxInstall.InstallPayload(LinuxInstall.LibDirectory, LinuxInstall.BinDirectory, executable);
        if (OperatingSystem.IsWindows())
            return WindowsInstall.InstallPayload(WindowsInstall.ProgramDirectory, executable);
        return null;
    }

    private static void InstallSupport(string binary, string version)
    {
        if (OperatingSystem.IsLinux())
            LinuxInstall.InstallSupport(LinuxInstall.DataHome, LinuxInstall.BinDirectory, binary, OpenAsset);
        else if (OperatingSystem.IsWindows())
            WindowsInstall.InstallSupport(WindowsInstall.StartMenuDirectory, binary, version, registerUninstall: true);
    }

    private static string InstalledBinary() =>
        OperatingSystem.IsLinux() ? LinuxInstall.BinaryPath(LinuxInstall.LibDirectory)
        : OperatingSystem.IsWindows() ? WindowsInstall.BinaryPath(WindowsInstall.ProgramDirectory)
        : string.Empty;

    private static string[] SupportFiles() =>
        OperatingSystem.IsLinux() ? LinuxInstall.SupportFiles(LinuxInstall.DataHome, LinuxInstall.BinDirectory)
        : OperatingSystem.IsWindows() ? WindowsInstall.SupportFiles(WindowsInstall.StartMenuDirectory)
        : [];

    private static bool IsEligible()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsWindows())
            return false;
        if (Environment.GetEnvironmentVariable(OptOutVariable) is { Length: > 0 })
            return false;
        return !IsDebugBuild();
    }

    private static bool IsDebugBuild() =>
        typeof(DesktopInstall).Assembly
            .GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration
            .Contains("Debug", StringComparison.OrdinalIgnoreCase) ?? false;

    private static Version RunningVersion() =>
        typeof(DesktopInstall).Assembly.GetName().Version ?? new Version(0, 0);

    private static byte[]? OpenAsset(string name)
    {
        try
        {
            using Stream? stream = typeof(DesktopInstall).Assembly.GetManifestResourceStream(name);
            if (stream is null)
                return null;
            using MemoryStream buffer = new();
            stream.CopyTo(buffer);
            return buffer.ToArray();
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or FileLoadException)
        {
            return null;
        }
    }
}
