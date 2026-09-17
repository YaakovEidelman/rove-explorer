using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Rove.Core.Protocol;

namespace Rove.Core.Services;

/// <summary>
/// Sends items to the OS trash so everyday delete is recoverable
/// (spec line 9: "Moves files to the Trash/Recycle Bin"), and brings them
/// back out again. Windows: one batched SHFileOperation with FOF_ALLOWUNDO,
/// undone through the shell. Linux: the desktop trash directory every file
/// manager there shares — see <see cref="LinuxTrash"/>.
/// </summary>
public static class TrashService
{
    public static bool IsSupported => OperatingSystem.IsWindows() || OperatingSystem.IsLinux();

    /// <summary>What to call it in front of the user on this OS.</summary>
    public static string DisplayName => OperatingSystem.IsWindows() ? "Recycle Bin" : "Trash";

    /// <summary>
    /// The trash as a folder to walk into, or null where the OS keeps it
    /// somewhere that is not one (Windows: the Recycle Bin is a shell folder,
    /// not a directory listing).
    /// </summary>
    public static string? BrowsePath => OperatingSystem.IsLinux() ? LinuxTrash.BrowsePath() : null;

    /// <summary>Puts back items that are in the trash now, by where they sit in it.</summary>
    public static CommandResult<OpResult[]> RestoreTrashedPaths(string[] paths)
    {
        if (!OperatingSystem.IsLinux())
        {
            return CommandResult<OpResult[]>.Fail("trash_unsupported",
                $"The {DisplayName} cannot be put back from here on this system.");
        }
        return CommandResult<OpResult[]>.Ok(LinuxTrash.RestoreTrashedPaths(paths));
    }

    /// <summary>Forgets the trash's record of an item that has now been deleted outright.</summary>
    public static void ForgetRecord(string path)
    {
        if (OperatingSystem.IsLinux())
            LinuxTrash.ForgetRecord(path);
    }

    public static CommandResult<string?> MoveToTrash(string[] paths)
    {
        if (paths.Length == 0)
            return CommandResult<string?>.Ok(null);
        if (OperatingSystem.IsLinux())
            return LinuxTrash.MoveToTrash(paths);
        if (!OperatingSystem.IsWindows())
            return CommandResult<string?>.Fail("trash_unsupported", "There is no trash to use on this system.");

        // SHFileOperation is the only batch Recycle Bin call, and it predates
        // extended paths: it ignores the \\?\ prefix and fails on
        // anything past MAX_PATH. Say so instead of half-deleting.
        if (paths.FirstOrDefault(LongPath.NeedsExtendedForm) is { } tooLong)
            return CommandResult<string?>.Fail(
                "path_too_long",
                $"The Recycle Bin cannot take paths longer than {LongPath.WindowsMaxPath} characters: {Path.GetFileName(tooLong)}. Delete it permanently instead.");

        return WindowsMoveToTrash(paths);
    }

    private const uint FO_DELETE = 0x0003;
    private const ushort FOF_ALLOWUNDO = 0x0040;
    private const ushort FOF_NOCONFIRMATION = 0x0010;
    private const ushort FOF_SILENT = 0x0004;
    private const ushort FOF_NOERRORUI = 0x0400;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEOPSTRUCTW
    {
        public IntPtr hwnd;
        public uint wFunc;
        public string pFrom;
        public string? pTo;
        public ushort fFlags;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        public string? lpszProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "SHFileOperationW")]
    private static extern int SHFileOperation(ref SHFILEOPSTRUCTW fileOp);

    [SupportedOSPlatform("windows")]
    private static CommandResult<string?> WindowsMoveToTrash(string[] paths)
    {
        // pFrom is a double-null-terminated list; the marshaler appends one
        // terminator, so we add the other explicitly.
        string from = string.Join('\0', paths.Select(Path.GetFullPath)) + "\0";

        SHFILEOPSTRUCTW op = new()
        {
            wFunc = FO_DELETE,
            pFrom = from,
            fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT | FOF_NOERRORUI,
        };

        int code = SHFileOperation(ref op);
        if (code != 0)
            return CommandResult<string?>.Fail("trash_failed", $"Shell returned error 0x{code:X}.");
        if (op.fAnyOperationsAborted)
            return CommandResult<string?>.Fail("trash_aborted", "The operation was aborted.");
        return CommandResult<string?>.Ok(null);
    }

    // ── putting things back ──────────────────────────────────────────────

    /// <summary>
    /// Puts previously trashed items back at the paths they were deleted
    /// from. Returns the paths that actually came back — an item the user
    /// already emptied out of the Recycle Bin simply isn't among them.
    /// </summary>
    public static CommandResult<string[]> RestoreFromTrash(string[] paths)
    {
        if (paths.Length == 0)
            return CommandResult<string[]>.Ok([]);
        if (OperatingSystem.IsLinux())
            return LinuxTrash.RestoreFromTrash(paths);
        if (OperatingSystem.IsWindows())
            return CommandResult<string[]>.Ok(WindowsTrash.Restore(paths));
        return CommandResult<string[]>.Fail("trash_unsupported", "There is no trash to use on this system.");
    }
}
