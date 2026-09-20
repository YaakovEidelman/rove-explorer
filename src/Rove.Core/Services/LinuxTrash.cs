using System.Runtime.Versioning;
using Rove.Core.Protocol;

namespace Rove.Core.Services;

[SupportedOSPlatform("linux")]
public static partial class LinuxTrash
{
    private const string FilesDir = "files";
    private const string InfoDir = "info";
    private const string InfoSuffix = ".trashinfo";

    private const int MaxNameAttempts = 1024;

    public static CommandResult<string?> MoveToTrash(string[] paths)
    {
        foreach (string path in paths)
        {
            CommandResult<string?> one = TrashOne(path);
            if (!one.IsOk)
                return one;
        }
        return CommandResult<string?>.Ok(null);
    }

    private static CommandResult<string?> TrashOne(string path)
    {
        string full = Path.GetFullPath(path);
        if (!File.Exists(full) && !Directory.Exists(full))
            return CommandResult<string?>.Fail("not_found", $"No such item: {path}");

        string? trash = ResolveTrashDirectory(full, out string? topDir, out string? problem);
        if (trash is null)
            return CommandResult<string?>.Fail("trash_unavailable", problem);

        try
        {
            Directory.CreateDirectory(Path.Combine(trash, FilesDir));
            Directory.CreateDirectory(Path.Combine(trash, InfoDir));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return CommandResult<string?>.Fail("trash_failed", $"Could not open the trash at {trash} ({ex.Message}).");
        }

        string recorded = topDir is null ? full : Path.GetRelativePath(topDir, full);

        return MoveIntoTrash(full, trash, recorded);
    }

    private static CommandResult<string?> MoveIntoTrash(string full, string trash, string recorded)
    {
        string name = Path.GetFileName(Path.TrimEndingDirectorySeparator(full));
        string stem = Path.GetFileNameWithoutExtension(name);
        string extension = Path.GetExtension(name);

        for (int attempt = 0; attempt < MaxNameAttempts; attempt++)
        {
            string candidate = attempt == 0 ? name : $"{stem}.{attempt}{extension}";
            string infoPath = Path.Combine(trash, InfoDir, candidate + InfoSuffix);
            string filePath = Path.Combine(trash, FilesDir, candidate);

            if (File.Exists(filePath) || Directory.Exists(filePath))
                continue;

            try
            {
                using (FileStream claim = new(infoPath, FileMode.CreateNew, FileAccess.Write))
                using (StreamWriter writer = new(claim))
                {
                    writer.Write(InfoContents(recorded, DateTime.Now));
                }
            }
            catch (IOException) when (File.Exists(infoPath))
            {
                continue;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return CommandResult<string?>.Fail("trash_failed", $"Could not write to the trash ({ex.Message}).");
            }

            try
            {
                Move(full, filePath);
                return CommandResult<string?>.Ok(null);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                TryDelete(infoPath);
                return CommandResult<string?>.Fail("trash_failed", $"Could not move {name} to the trash ({ex.Message}).");
            }
        }

        return CommandResult<string?>.Fail("trash_failed", $"The trash already holds too many things named {name}.");
    }

    internal static string InfoContents(string recordedPath, DateTime deletedAt) =>
        $"[Trash Info]\nPath={EncodePath(recordedPath)}\nDeletionDate={deletedAt:yyyy-MM-ddTHH:mm:ss}\n";
}
