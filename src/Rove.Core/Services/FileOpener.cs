using System.ComponentModel;
using System.Diagnostics;

namespace Rove.Core.Services;

/// <summary>
/// Hands a file to whatever the desktop opens it with.
///
/// <para>
/// Windows has one answer and the OS knows it. Linux has several, none of
/// them guaranteed to be installed, and the one that is there can still come
/// back saying nothing on this system opens that kind of file — quietly,
/// because it says so by exiting with a code rather than by failing to start.
/// Handing the path to the runtime's own "shell execute" hides both of those:
/// with no opener installed it tries to <em>run</em> the file, and with one
/// installed it never looks at what the opener said. So the opener is chosen
/// here, and its answer is waited for.
/// </para>
///
/// <para>
/// The answer has to be collected without lending the opener a pipe. An
/// opener does not open the file itself; it starts the program that does and
/// leaves, and that program inherits the opener's three standard streams and
/// keeps them for as long as it runs. A pipe from this process would outlive
/// the opener in the hands of the program, and closing this end of it — which
/// is what disposing the opener does, moments after it exits — leaves the
/// program writing into a pipe with no reader, so the first word it says to
/// standard error kills it. A viewer that says nothing survives; an editor or
/// a terminal does not, which is why images opened and text did not. So the
/// streams handed over are a file and <c>/dev/null</c>: the program can hold
/// them for as long as it likes and nothing this process does later can pull
/// them out from under it.
/// </para>
/// </summary>
public static class FileOpener
{
    /// <summary>
    /// How long an opener gets to refuse before it is taken to have worked.
    /// The tools that hand off and exit do so at once; the ones that stay
    /// alive for as long as the program they started are still running when
    /// this runs out, which is the same thing as success.
    /// </summary>
    private static readonly TimeSpan _refusalWindow = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Run the opener with streams that outlive this process's interest in
    /// them: nothing to read, output thrown away, and complaints appended to
    /// the file named by <c>$0</c>. <c>exec</c> keeps this the opener's own
    /// process, so the exit code waited for is still the opener's. The words
    /// are a constant and the file and the arguments arrive as arguments, so
    /// there is nothing here for a filename to break out of.
    /// </summary>
    private const string _handOff = "exec \"$@\" </dev/null >/dev/null 2>\"$0\"";

    /// <summary>
    /// The shell that arranges those streams. POSIX puts it here and every
    /// Linux has one, but if it is somehow missing the opener is run as it
    /// comes: it still opens the file, and only its complaint is lost.
    /// </summary>
    private const string _shell = "/bin/sh";

    /// <summary>
    /// The programs that know the desktop's file associations, best first.
    /// <c>gio</c> goes first: it launches a terminal-only default application
    /// (an editor with no windows of its own, say) inside a terminal, trying
    /// <c>xdg-terminal-exec</c> and then a list of known ones. <c>xdg-open</c>
    /// does not do this on window managers it does not recognize by name, so
    /// a file whose default handler wants a terminal would open silently into
    /// nothing. It is also the only one that reports having found no handler:
    /// <c>xdg-open</c> exits 0 either way. The rest are what particular
    /// desktops ship, and <c>wslview</c> is what a WSL session has instead.
    /// </summary>
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

    /// <summary>
    /// A running opener and the file it was given to complain into. Disposing
    /// it drops both; the program the opener started keeps its own handle on
    /// the file, and unlinking a file it still holds open takes nothing away
    /// from it.
    /// </summary>
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

    /// <summary>
    /// Starts the desktop's opener on <paramref name="path"/>. Returns null
    /// when it started, or a line for the user when it could not. The started
    /// opener, where there is one, is the caller's to dispose.
    /// </summary>
    public static string? Start(string path, out Opener? opener)
    {
        opener = null;
        if (OperatingSystem.IsWindows())
            return StartWindows(path);

        if (FindOpener(Environment.GetEnvironmentVariable("PATH"), IsRunnable) is not { } found)
            return "There is no program on this system for opening files. Installing xdg-utils gives Rove one.";

        string[] leading = found.First is { Length: > 0 } first ? [first] : [];
        return Run(found.Path, leading, path, out opener);
    }

    /// <summary>
    /// Same, but with the app the user picked rather than the one the desktop
    /// would. <c>gio launch</c> is what runs a .desktop file, so it is the one
    /// program this needs.
    /// </summary>
    public static string? StartWith(string desktopFile, string path, out Opener? opener)
    {
        opener = null;
        if (OperatingSystem.IsWindows())
            return "Windows asks which app to use on its own.";

        if (FindProgram(Environment.GetEnvironmentVariable("PATH"), "gio", IsRunnable) is not { } gio)
            return "Opening a file with a chosen app needs gio, which comes with glib.";

        return Run(gio, ["launch", desktopFile], path, out opener);
    }

    private static string? Run(string program, string[] leading, string path, out Opener? opener)
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
        foreach (string argument in leading)
            info.ArgumentList.Add(argument);
        info.ArgumentList.Add(LongPath.Display(path));

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

    /// <summary>
    /// Waits out the refusal window and reports what the opener said, or null
    /// when it said nothing — which includes still being busy saying nothing.
    /// </summary>
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

    /// <summary>What the opener wrote to standard error, if anything.</summary>
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

    /// <summary>
    /// What to tell the user when the opener exited unhappy. Its own words
    /// come first when it had any; the codes are xdg-open's, and the one worth
    /// naming is "nothing here handles this kind of file".
    /// </summary>
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

    /// <summary>
    /// The part of an opener's complaint worth repeating: the first line,
    /// with the "who I am and which file" preamble taken off. <c>gio</c>
    /// prefixes its own name and the file's URI, which the user is already
    /// looking at, and the sentence after it is the part that says why.
    /// </summary>
    internal static string Spoken(string complaint)
    {
        string line = complaint.Split('\n')[0].Trim();
        int uri = line.IndexOf("file://", StringComparison.Ordinal);
        if (uri < 0)
            return line;

        int after = line.IndexOf(": ", uri, StringComparison.Ordinal);
        return after < 0 ? line : line[(after + 2)..].Trim();
    }

    /// <summary>
    /// First opener present in <paramref name="pathVariable"/>. Split out
    /// from the starting so the choosing can be tested without running
    /// anything: <paramref name="exists"/> stands in for the filesystem.
    /// </summary>
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

    /// <summary>
    /// Whether a candidate is a file this process could actually run. A name
    /// on the PATH that is present but not executable is not an opener, and
    /// treating it as one would stop the search at something that can only
    /// fail.
    /// </summary>
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
