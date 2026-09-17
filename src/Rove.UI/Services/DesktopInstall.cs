using Rove.Core.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Rove.UI.Services;

/// <summary>
/// Rove installs itself. Downloading a build and running it is the whole
/// procedure: the first launch copies the binary where the OS keeps programs,
/// writes whatever the desktop needs to show it, and notes what it did. A
/// later launch of a newer build replaces the installed one.
///
/// <para>
/// The note it leaves — one small JSON file — is what keeps this off the
/// startup path. A launch reads it, sees its own build described, and stops.
/// That check, and everything past it, happens on a background thread anyway,
/// so the window never waits for any of it.
/// </para>
///
/// <para>
/// An older build never replaces a newer install: whatever is installed is
/// the newest thing that has run, which is how every self-updating program
/// behaves. Running an old copy on purpose is fine — it just does not drag
/// the install backwards. <c>--install</c> forces it when that is what you
/// actually want.
/// </para>
/// </summary>
public static class DesktopInstall
{
    /// <summary>Set this to anything and Rove will not install itself.</summary>
    public const string OptOutVariable = "ROVE_NO_INSTALL";

    /// <summary>Starts the check off the UI thread and forgets about it.</summary>
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
                // An install that cannot happen is not a reason to stop the app.
            }
        });
    }

    /// <summary>Installs the running build, newer or not. The answer is for a terminal.</summary>
    public static string InstallNow()
    {
        if (Environment.ProcessPath is null)
            return "Rove could not work out where it is running from.";
        InstallStep step = Run(force: true);
        return step == InstallStep.Nothing
            ? $"Rove is already installed at {InstalledBinary()}."
            : $"Rove is installed at {InstalledBinary()}.";
    }

    /// <summary>
    /// Takes the install back out again, and says so before it does the one
    /// part that cannot be taken back: removing the binary. That binary is
    /// often the one running this, and a program that has deleted itself is
    /// in no state to do anything else — on Windows it cannot even be
    /// deleted, so both cases hand the last step to a small detached command.
    /// </summary>
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

        // A build is a folder of files on both systems, so the folder goes.
        if (self)
            SelfDelete.After(Path.GetDirectoryName(installed) ?? string.Empty);
        else
            RemoveInstalled();
    }

    private static void RemoveInstalled()
    {
        if (OperatingSystem.IsLinux())
            LinuxInstall.RemoveInstalled(LinuxInstall.LibDirectory, LinuxInstall.BinDirectory);
        else if (OperatingSystem.IsWindows())
            WindowsInstall.RemoveInstalled(WindowsInstall.ProgramDirectory);
    }

    // ── deciding ─────────────────────────────────────────────────────────

    /// <summary>
    /// What this launch has to do. Kept apart from the doing so the rule —
    /// newest build wins, and nothing is written when it already has — is one
    /// readable thing.
    /// </summary>
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

    // ── doing ────────────────────────────────────────────────────────────

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

        // The record describes the build that is installed, so a repair that
        // could not replace the binary must not claim the running one is it.
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

    // ── when not to ──────────────────────────────────────────────────────

    /// <summary>
    /// A build from the source tree stays out of the way: installing whatever
    /// was last compiled, every time it is run, is nobody's idea of helpful.
    /// </summary>
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

    /// <summary>
    /// The icons come straight out of the assembly, not through Avalonia's
    /// asset loader: --install and --uninstall never start a UI, and there
    /// would be no loader to ask.
    /// </summary>
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
