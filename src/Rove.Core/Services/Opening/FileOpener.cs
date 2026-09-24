using System.ComponentModel;
using System.Diagnostics;

namespace Rove.Core.Services;

public static class FileOpener
{
    private static readonly TimeSpan _refusalWindow = TimeSpan.FromSeconds(3);

    private const string _handOff = "exec \"$@\" </dev/null >/dev/null 2>\"$0\"";

    private const string _shell = "/bin/sh";

    private static readonly (string Program, string? First)[] _openers =
    [
        ("gio", "open"),
        ("xdg-open", null),
        ("gnome-open", null),
        ("kde-open", null),
        ("kde-open5", null),
        ("exo-open", null),
        ("wslview", null),
    ];

    public sealed class Opener(Process process, string? errorFile) : IDisposable
    {
        internal Process Process { get; } = process;

        internal string? ErrorFile { get; } = errorFile;

        public void Dispose()
        {
            Process.Dispose();
            Discard(ErrorFile);
        }
    }

    public static string? Start(string path, out Opener? opener)
    {
        opener = null;
        if (OperatingSystem.IsWindows())
            return StartWindows(path);

        if (IsNativeExecutable(path))
            return Run(LongPath.Display(path), [], Path.GetDirectoryName(LongPath.Display(path)), out opener);

        if (FindOpener(Environment.GetEnvironmentVariable("PATH"), IsRunnable) is not { } found)
            return "There is no program on this system for opening files. Installing xdg-utils gives Rove one.";

        string[] arguments = found.First is { Length: > 0 } first
            ? [first, LongPath.Display(path)]
            : [LongPath.Display(path)];
        return Run(found.Path, arguments, null, out opener);
    }

    public static string? StartWith(string desktopFile, string path, out Opener? opener)
    {
        opener = null;
        if (OperatingSystem.IsWindows())
            return "Windows asks which app to use on its own.";

        if (FindProgram(Environment.GetEnvironmentVariable("PATH"), "gio", IsRunnable) is not { } gio)
            return "Opening a file with a chosen app needs gio, which comes with glib.";

        return Run(gio, ["launch", desktopFile, LongPath.Display(path)], null, out opener);
    }

    private static string? Run(string program, string[] arguments, string? workingDirectory, out Opener? opener)
    {
        opener = null;
        string? errorFile = File.Exists(_shell)
            ? Path.Combine(Path.GetTempPath(), $"rove-open-{Guid.NewGuid():N}")
            : null;

        ProcessStartInfo info = new(errorFile is null ? program : _shell) { UseShellExecute = false };
        if (errorFile is not null)
        {
            info.ArgumentList.Add("-c");
            info.ArgumentList.Add(_handOff);
            info.ArgumentList.Add(errorFile);
            info.ArgumentList.Add(program);
        }
        foreach (string argument in arguments)
            info.ArgumentList.Add(argument);
        if (workingDirectory is { Length: > 0 })
            info.WorkingDirectory = workingDirectory;

        try
        {
            opener = new Opener(Process.Start(info)!, errorFile);
            return null;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            Discard(errorFile);
            return ex.Message;
        }
    }

    public static async Task<string?> WaitForRefusal(Opener? opener, string path, CancellationToken ct = default)
    {
        if (opener is null)
            return null;

        using CancellationTokenSource window = CancellationTokenSource.CreateLinkedTokenSource(ct);
        window.CancelAfter(_refusalWindow);
        try
        {
            await opener.Process.WaitForExitAsync(window.Token);
        }
        catch (OperationCanceledException)
        {
            return null;
        }

        if (opener.Process.ExitCode == 0)
            return null;

        return Refusal(
            opener.Process.ExitCode,
            Complaint(opener.ErrorFile),
            Path.GetFileName(LongPath.Display(path)));
    }

    private static string Complaint(string? errorFile)
    {
        if (errorFile is null)
            return string.Empty;
        try
        {
            return File.ReadAllText(errorFile).Trim();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }

    private static void Discard(string? errorFile)
    {
        if (errorFile is null)
            return;
        try
        {
            File.Delete(errorFile);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    internal static string Refusal(int exitCode, string complaint, string name)
    {
        if (Spoken(complaint) is { Length: > 0 } said)
            return $"Could not open {name}: {said}";
        return exitCode switch
        {
            3 => $"Nothing on this system is set up to open {name}.",
            4 => $"The program that opens {name} would not start.",
            _ => $"Could not open {name}.",
        };
    }

    internal static string Spoken(string complaint)
    {
        string line = complaint.Split('\n')[0].Trim();
        int uri = line.IndexOf("file://", StringComparison.Ordinal);
        if (uri < 0)
            return line;

        int after = line.IndexOf(": ", uri, StringComparison.Ordinal);
        return after < 0 ? line : line[(after + 2)..].Trim();
    }

    internal static (string Path, string? First)? FindOpener(string? pathVariable, Func<string, bool> exists)
    {
        foreach ((string program, string? first) in _openers)
        {
            if (FindProgram(pathVariable, program, exists) is { } found)
                return (found, first);
        }
        return null;
    }

    internal static string? FindProgram(string? pathVariable, string program, Func<string, bool> exists)
    {
        string[] directories = (pathVariable ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (string directory in directories)
        {
            string candidate = Path.Combine(directory, program);
            if (exists(candidate))
                return candidate;
        }
        return null;
    }

    internal static bool IsNativeExecutable(string path)
    {
        if (OperatingSystem.IsWindows())
            return false;

        const UnixFileMode executable =
            UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
        byte[] elf = [0x7F, (byte)'E', (byte)'L', (byte)'F'];
        try
        {
            string real = LongPath.Display(path);
            if ((File.GetUnixFileMode(real) & executable) == 0)
                return false;

            using FileStream stream = File.OpenRead(real);
            Span<byte> head = stackalloc byte[4];
            return stream.ReadAtLeast(head, head.Length, throwOnEndOfStream: false) == head.Length
                && head.SequenceEqual(elf);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            return false;
        }
    }

    internal static bool IsRunnable(string candidate)
    {
        try
        {
            if (!File.Exists(candidate))
                return false;
            if (OperatingSystem.IsWindows())
                return true;

            const UnixFileMode executable =
                UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
            return (File.GetUnixFileMode(candidate) & executable) != 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            return false;
        }
    }

    private static string? StartWindows(string path)
    {
        try
        {
            using Process? started = Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            return null;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            return ex.Message;
        }
    }
}
