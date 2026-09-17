using Rove.Core.Protocol;

namespace Rove.Core.Services;

public sealed record SearchHit(FolderItem Item, int Score);

/// <summary>
/// SEARCH_GLOBAL: deep fuzzy search from a root directory downwards.
/// Cancellable, skips inaccessible/hidden/system entries and reparse points
/// (no cycles), and caps how many entries it visits so a search of C:\ can't
/// run away.
/// </summary>
public class GlobalSearchService
{
    private const int MaxVisitedEntries = 250_000;

    public Task<CommandResult<SearchHit[]>> SearchAsync(
        string root, string query, int maxResults, CancellationToken ct
    ) => Task.Run(() => Search(root, query, maxResults, ct), ct);

    private static CommandResult<SearchHit[]> Search(
        string root, string query, int maxResults, CancellationToken ct
    )
    {
        if (string.IsNullOrWhiteSpace(query))
            return CommandResult<SearchHit[]>.Ok([]);
        if (!Directory.Exists(root))
            return CommandResult<SearchHit[]>.Fail("not_found", $"Directory does not exist: {root}");

        EnumerationOptions options = new()
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReparsePoint,
        };

        List<SearchHit> hits = [];
        int visited = 0;
        bool truncated = false;

        try
        {
            DirectoryInfo dir = new(root);
            foreach (FileSystemInfo info in dir.EnumerateFileSystemInfos("*", options))
            {
                if (ct.IsCancellationRequested)
                    break;
                if (++visited > MaxVisitedEntries)
                {
                    truncated = true;
                    break;
                }
                if (FuzzyMatcher.TryMatch(query, info.Name, out int score))
                    hits.Add(new SearchHit(FolderItem.From(info), score));
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            // IgnoreInaccessible covers children; the root itself can still throw.
            return CommandResult<SearchHit[]>.Fail("permission_denied", ex.Message);
        }

        SearchHit[] top = [.. hits.OrderByDescending(h => h.Score).Take(Math.Max(1, maxResults))];
        return truncated
            ? CommandResult<SearchHit[]>.Fail("truncated", "Search stopped early; results may be incomplete.", top)
            : CommandResult<SearchHit[]>.Ok(top);
    }
}
