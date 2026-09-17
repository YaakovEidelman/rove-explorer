using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Runtime.InteropServices.Marshalling;
using System.Text;
using System.Threading;

namespace Rove.UI.Services;

/// <summary>
/// Puts Rove where Windows looks for a program installed by one person rather
/// than for the whole machine: the executable under
/// <c>%LOCALAPPDATA%\Programs\Rove</c>, a Start Menu shortcut, and an entry
/// in Installed Apps that uninstalls it again. None of that needs
/// administrator rights, which is the point — the download runs, and it is
/// installed.
/// </summary>
[SupportedOSPlatform("windows")]
internal static partial class WindowsInstall
{
    public const string AppName = "Rove";

    private const string ExeName = "Rove.exe";
    private const string ShortcutName = AppName + ".lnk";
    private const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Rove";

    public static string ProgramDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", AppName);

    public static string StartMenuDirectory =>
        Environment.GetFolderPath(Environment.SpecialFolder.Programs);

    public static string BinaryPath(string programDir) => Path.Combine(programDir, ExeName);

    public static string ShortcutPath(string startMenuDir) => Path.Combine(startMenuDir, ShortcutName);

    public static string[] SupportFiles(string startMenuDir) => [ShortcutPath(startMenuDir)];

    /// <summary>
    /// Copies the build the caller is running from into the program folder.
    /// Windows will not delete a program that is running, but it will rename
    /// one, so each file is pushed aside rather than overwritten.
    /// </summary>
    public static string? InstallPayload(string programDir, string executable)
    {
        string target = BinaryPath(programDir);
        if (string.Equals(Path.GetFullPath(executable), Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase))
            return target;

        if (Path.GetDirectoryName(Path.GetFullPath(executable)) is not { Length: > 0 } source)
            return null;
        return Payload.Install(source, programDir, ExeName) ? target : null;
    }

    public static bool InstallSupport(string startMenuDir, string binary, string version, bool registerUninstall)
    {
        bool wrote = WriteShortcut(ShortcutPath(startMenuDir), binary);
        if (registerUninstall)
            wrote |= RegisterUninstall(binary, version);
        return wrote;
    }

    public static void Uninstall(string programDir, string startMenuDir, bool unregister)
    {
        UninstallSupport(startMenuDir, unregister);
        RemoveInstalled(programDir);
    }

    /// <summary>The shortcut and the Installed Apps entry — everything but the exe.</summary>
    public static void UninstallSupport(string startMenuDir, bool unregister)
    {
        TryDelete(ShortcutPath(startMenuDir));
        if (unregister)
            DeleteKey(UninstallKey);
    }

    /// <summary>Removes an installed build, folder and all — it is Rove's own.</summary>
    public static void RemoveInstalled(string programDir) => Payload.Remove(programDir);

    // ── the Start Menu shortcut ──────────────────────────────────────────
    // A .lnk is a shell object, not a file format anything here can write, so
    // this asks the shell to make one. The interop is generated at compile
    // time rather than discovered at runtime, because a natively compiled
    // build has no way to build the plumbing for a COM call as it goes.

    private static bool WriteShortcut(string shortcut, string binary)
    {
        bool written = false;
        RunInSingleThreadedApartment(() =>
        {
            try
            {
                if (Path.GetDirectoryName(shortcut) is { Length: > 0 } parent)
                    Directory.CreateDirectory(parent);

                using ComObject shell = ComObject.Create(ShellLinkClsid);
                IShellLinkW link = shell.As<IShellLinkW>();
                link.SetPath(binary);
                link.SetWorkingDirectory(Path.GetDirectoryName(binary) ?? string.Empty);
                link.SetDescription("Keyboard-driven file explorer");
                link.SetIconLocation(binary, 0);
                shell.As<IPersistFile>().Save(shortcut, true);
                written = true;
            }
            catch (Exception ex) when (ex is COMException or InvalidCastException or NotSupportedException
                                          or IOException or UnauthorizedAccessException)
            {
            }
        });
        return written;
    }

    private static void RunInSingleThreadedApartment(Action action)
    {
        Thread thread = new(() =>
        {
            try
            {
                action();
            }
            catch (Exception)
            {
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        thread.Join();
    }

    private static readonly Guid ShellLinkClsid = new("00021401-0000-0000-C000-000000000046");

    /// <summary>
    /// One shell object, and the interfaces asked of it, released together.
    /// Wrapping the raw pointer by hand is what replaces <c>new SomeComClass()</c>:
    /// that syntax needs the runtime to invent a wrapper type on the spot.
    /// </summary>
    private sealed class ComObject : IDisposable
    {
        private static readonly StrategyBasedComWrappers _wrappers = new();

        private readonly object _instance;

        private ComObject(object instance) => _instance = instance;

        public static ComObject Create(Guid clsid)
        {
            Guid unknown = new("00000000-0000-0000-C000-000000000046");
            int hr = CoCreateInstance(in clsid, IntPtr.Zero, ClsCtxInprocServer, in unknown, out IntPtr raw);
            if (hr < 0 || raw == IntPtr.Zero)
                throw new COMException("The shell would not create a shortcut object.", hr);

            try
            {
                return new ComObject(_wrappers.GetOrCreateObjectForComInstance(raw, CreateObjectFlags.UniqueInstance));
            }
            finally
            {
                Marshal.Release(raw);
            }
        }

        public T As<T>() => (T)_instance;

        public void Dispose()
        {
            if (_instance is IDisposable disposable)
                disposable.Dispose();
        }
    }

    private const int ClsCtxInprocServer = 1;

    [DllImport("ole32.dll")]
    private static extern int CoCreateInstance(
        in Guid clsid, IntPtr outer, int context, in Guid iid, out IntPtr instance);

    [GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    internal partial interface IShellLinkW
    {
        // Only the setters are ever called. The getters still have to be
        // here, in this order — the slots are how a COM call finds a method —
        // and their buffers stay raw pointers so nothing has to be marshalled
        // for a call that never happens.
        void GetPath(IntPtr file, int chars, IntPtr findData, uint flags);
        void GetIDList(out IntPtr idList);
        void SetIDList(IntPtr idList);
        void GetDescription(IntPtr name, int chars);
        void SetDescription(string name);
        void GetWorkingDirectory(IntPtr directory, int chars);
        void SetWorkingDirectory(string directory);
        void GetArguments(IntPtr arguments, int chars);
        void SetArguments(string arguments);
        void GetHotkey(out short hotkey);
        void SetHotkey(short hotkey);
        void GetShowCmd(out int show);
        void SetShowCmd(int show);
        void GetIconLocation(IntPtr icon, int chars, out int index);
        void SetIconLocation(string icon, int index);
        void SetRelativePath(string path, uint reserved);
        void Resolve(IntPtr window, uint flags);
        void SetPath(string file);
    }

    [GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
    [Guid("0000010b-0000-0000-C000-000000000046")]
    internal partial interface IPersistFile
    {
        void GetClassID(out Guid classId);
        [PreserveSig] int IsDirty();
        void Load(string fileName, uint mode);
        void Save(string? fileName, [MarshalAs(UnmanagedType.Bool)] bool remember);
        void SaveCompleted(string fileName);
        void GetCurFile(out IntPtr fileName);
    }

    // ── Installed Apps ───────────────────────────────────────────────────
    // The registry values Windows reads to list a program and offer to remove
    // it. Under HKEY_CURRENT_USER, so this is one person's install and needs
    // nobody's permission.

    private static bool RegisterUninstall(string binary, string version)
    {
        string directory = Path.GetDirectoryName(binary) ?? ProgramDirectory;
        long size = 0;
        try
        {
            size = new FileInfo(binary).Length / 1024;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }

        Dictionary<string, string> values = new(StringComparer.Ordinal)
        {
            ["DisplayName"] = AppName,
            ["DisplayVersion"] = version,
            ["DisplayIcon"] = binary,
            ["InstallLocation"] = directory,
            ["Publisher"] = AppName,
            ["UninstallString"] = $"\"{binary}\" --uninstall",
            ["QuietUninstallString"] = $"\"{binary}\" --uninstall",
        };

        return WriteKey(UninstallKey, values, ("EstimatedSize", (int)size), ("NoModify", 1), ("NoRepair", 1));
    }

    private const uint HKEY_CURRENT_USER = 0x80000001;
    private const int KEY_WRITE = 0x20006;
    private const int REG_SZ = 1;
    private const int REG_DWORD = 4;

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int RegCreateKeyExW(
        UIntPtr key, string subKey, int reserved, string? className, int options,
        int desired, IntPtr security, out IntPtr result, out int disposition);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int RegSetValueExW(
        IntPtr key, string name, int reserved, int type, byte[] data, int size);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern int RegCloseKey(IntPtr key);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int RegDeleteTreeW(UIntPtr key, string subKey);

    private static bool WriteKey(
        string subKey,
        Dictionary<string, string> strings,
        params (string Name, int Value)[] numbers
    )
    {
        IntPtr handle = IntPtr.Zero;
        try
        {
            if (RegCreateKeyExW(new UIntPtr(HKEY_CURRENT_USER), subKey, 0, null, 0, KEY_WRITE, IntPtr.Zero,
                    out handle, out _) != 0)
            {
                return false;
            }

            foreach (KeyValuePair<string, string> entry in strings)
            {
                byte[] data = Encoding.Unicode.GetBytes(entry.Value + '\0');
                RegSetValueExW(handle, entry.Key, 0, REG_SZ, data, data.Length);
            }
            foreach ((string name, int value) in numbers)
                RegSetValueExW(handle, name, 0, REG_DWORD, BitConverter.GetBytes(value), sizeof(int));
            return true;
        }
        finally
        {
            if (handle != IntPtr.Zero)
                RegCloseKey(handle);
        }
    }

    private static void DeleteKey(string subKey) =>
        RegDeleteTreeW(new UIntPtr(HKEY_CURRENT_USER), subKey);

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
