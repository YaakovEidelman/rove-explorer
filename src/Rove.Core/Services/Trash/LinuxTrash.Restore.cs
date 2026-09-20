using Rove.Core.Protocol;

namespace Rove.Core.Services;

public static partial class LinuxTrash
{
    public static CommandResult<string[]> RestoreFromTrash(string[] paths)
    {
        List<string> restored = [];

        foreach (string path in paths)
        {
            string full = Path.GetFullPath(path);
            foreach (string trash in TrashesToSearch(full))
            {
                if (FindInTrash(trash, full) is not { } found)
                    continue;
                if (RestoreOne(found.FilePath, found.InfoPath, full))
                    restored.Add(path);
                break;
            }
        }

        return CommandResult<string[]>.Ok([.. restored]);
    }

    private static bool RestoreOne(string filePath, string infoPath, string destination)
    {
        if (File.Exists(destination) || Directory.Exists(destination))
            return false;

        try
        {
            if (Path.GetDirectoryName(destination) is { Length: > 0 } parent)
                Directory.CreateDirectory(parent);
            Move(filePath, destination);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }

        TryDelete(infoPath);
        return true;
    }

    private static (string FilePath, string InfoPath)? FindInTrash(string trash, string original)
    {
        string infoRoot = Path.Combine(trash, InfoDir);
        if (!Directory.Exists(infoRoot))
            return null;

        string? topDir = TopDirectoryOfTrash(trash);

        IEnumerable<string> infoFiles;
        try
        {
            infoFiles = Directory.EnumerateFiles(infoRoot, "*" + InfoSuffix);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        foreach (string infoPath in infoFiles)
        {
            if (ReadRecordedPath(infoPath) is not { } recorded)
                continue;

            string was = Path.IsPathRooted(recorded) || topDir is null
                ? recorded
                : Path.GetFullPath(Path.Combine(topDir, recorded));
            if (!PathCompare.PathMatches(was, original))
                continue;

            string name = Path.GetFileName(infoPath)[..^InfoSuffix.Length];
            string filePath = Path.Combine(trash, FilesDir, name);
            if (File.Exists(filePath) || Directory.Exists(filePath))
                return (filePath, infoPath);
        }
        return null;
    }

    internal static string? ReadRecordedPath(string infoPath)
    {
        try
        {
            foreach (string line in File.ReadLines(infoPath))
            {
                if (line.StartsWith("Path=", StringComparison.Ordinal))
                    return DecodePath(line["Path=".Length..].Trim());
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
        return null;
    }

    private static IEnumerable<string> TrashesToSearch(string full)
    {
        yield return HomeTrash();

        if (VolumeOf(full) is not { } volume || IsHomeVolume(volume))
            yield break;
        foreach (string candidate in VolumeTrashCandidates(volume))
            yield return candidate;
    }

    public static string BrowsePath() => Path.Combine(HomeTrash(), FilesDir);

    public static OpResult[] RestoreTrashedPaths(string[] paths) =>
        [.. paths.Select(RestoreTrashedPath)];

    private static OpResult RestoreTrashedPath(string path)
    {
        string full = Path.GetFullPath(path);
        string name = Path.GetFileName(Path.TrimEndingDirectorySeparator(full));

        if (!File.Exists(full) && !Directory.Exists(full))
            return OpResult.Failure(path, "not_found", $"No such item: {path}");

        if (TrashHolding(full) is not { } trash)
        {
            return OpResult.Failure(path, "not_in_trash",
                $"{name} is not a whole item in the trash, so there is nothing saying where it belongs.");
        }

        string infoPath = Path.Combine(trash, InfoDir, name + InfoSuffix);
        if (ReadRecordedPath(infoPath) is not { } recorded)
        {
            return OpResult.Failure(path, "no_record",
                $"The trash has no record of where {name} came from, so it can only be moved by hand.");
        }

        string? topDir = TopDirectoryOfTrash(trash);
        string destination = Path.IsPathRooted(recorded) || topDir is null
            ? recorded
            : Path.GetFullPath(Path.Combine(topDir, recorded));

        if (File.Exists(destination) || Directory.Exists(destination))
        {
            return OpResult.Failure(path, "already_exists",
                $"Something is at {destination} again, so {name} was left in the trash.");
        }

        if (!RestoreOne(full, infoPath, destination))
            return OpResult.Failure(path, "restore_failed", $"{name} could not be moved back to {destination}.");

        return OpResult.Success(path, FolderItem.FromPath(destination));
    }

    public static void ForgetRecord(string path)
    {
        string full = Path.GetFullPath(path);
        if (TrashHolding(full) is not { } trash)
            return;
        string name = Path.GetFileName(Path.TrimEndingDirectorySeparator(full));
        TryDelete(Path.Combine(trash, InfoDir, name + InfoSuffix));
    }
}
