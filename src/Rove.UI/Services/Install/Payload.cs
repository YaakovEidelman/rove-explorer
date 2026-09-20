namespace Rove.UI.Services;

internal static class Payload
{
    private const string StagedSuffix = ".new";
    private const string PreviousSuffix = ".old";

    private static readonly string[] _libraryExtensions = [".dll", ".so", ".dylib"];

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

    private static bool IsLibrary(string name)
    {
        if (_libraryExtensions.Contains(Path.GetExtension(name), StringComparer.OrdinalIgnoreCase))
            return true;
        return name.Contains(".so.", StringComparison.Ordinal);
    }

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
            TryDelete(previous);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            TryDelete(staged);
            return false;
        }
    }

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
