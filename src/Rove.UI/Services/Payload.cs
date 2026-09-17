using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Rove.UI.Services;

/// <summary>
/// What a build of Rove actually is on disk: the program, plus the handful of
/// native libraries it draws with. A compiled-ahead-of-time build cannot fold
/// those into one file the way the old bundled build did, so installing means
/// copying a small set of files rather than one.
///
/// <para>
/// Each file goes down under a temporary name and is then renamed into place.
/// A rename is atomic and — on both Windows and Linux — is allowed even while
/// the old file is in use, so an update can replace a copy that is running:
/// the name moves, and the running program keeps the file it opened.
/// </para>
/// </summary>
internal static class Payload
{
    private const string StagedSuffix = ".new";
    private const string PreviousSuffix = ".old";

    /// <summary>The libraries a build carries, whatever the system calls them.</summary>
    private static readonly string[] _libraryExtensions = [".dll", ".so", ".dylib"];

    /// <summary>
    /// The files that make up the build sitting in <paramref name="directory"/>:
    /// the program named by <paramref name="executable"/>, and the libraries
    /// beside it. Nothing else — someone who unpacks a download straight into
    /// a folder full of other things should not have that folder installed.
    /// </summary>
    public static string[] Files(string directory, string executable, IReadOnlyList<string>? extraFiles = null)
    {
        try
        {
            if (!File.Exists(Path.Combine(directory, executable)))
                return [];

            return
            [
                executable,
                .. (extraFiles ?? []).Where(name => File.Exists(Path.Combine(directory, name))),
                .. Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly)
                    .Select(Path.GetFileName)
                    .Where(name => name is not null && IsLibrary(name))
                    .Select(name => name!)
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase),
            ];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>
    /// A library, including the versioned kind Linux writes as
    /// <c>libfoo.so.1.2</c> — where the extension is a number, not ".so".
    /// </summary>
    private static bool IsLibrary(string name)
    {
        if (_libraryExtensions.Contains(Path.GetExtension(name), StringComparer.OrdinalIgnoreCase))
            return true;
        return name.Contains(".so.", StringComparison.Ordinal);
    }

    /// <summary>Copies every file of a build into <paramref name="targetDir"/>, replacing what is there.</summary>
    public static bool Install(
        string sourceDir, string targetDir, string executable, IReadOnlyList<string>? extraFiles = null)
    {
        string[] files = Files(sourceDir, executable, extraFiles);
        if (files.Length == 0)
            return false;

        try
        {
            Directory.CreateDirectory(targetDir);
            foreach (string name in files)
            {
                if (!InstallOne(Path.Combine(sourceDir, name), Path.Combine(targetDir, name)))
                    return false;
            }
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return false;
        }
    }

    private static bool InstallOne(string source, string target)
    {
        string staged = target + StagedSuffix;
        string previous = target + PreviousSuffix;
        try
        {
            if (Path.GetDirectoryName(target) is { Length: > 0 } parent)
                Directory.CreateDirectory(parent);

            TryDelete(previous);
            File.Copy(source, staged, overwrite: true);
            if (File.Exists(target))
                File.Move(target, previous, overwrite: true);
            File.Move(staged, target, overwrite: true);
            TryDelete(previous); // in use by a running copy; cleared on a later launch
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            TryDelete(staged);
            return false;
        }
    }

    /// <summary>Removes an installed build, and the folder it was in.</summary>
    public static void Remove(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

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
