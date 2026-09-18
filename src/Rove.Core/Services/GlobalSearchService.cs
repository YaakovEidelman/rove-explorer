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

        EnumerationOptions fastOptions = new()
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReparsePoint,
        };
        EnumerationOptions flatOptions = new()
        {
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReparsePoint,
        };

        List<SearchHit> hits = [];
        int visited = 0;
        bool truncated = false;
        string? failureMessage = null;

        void TryAdd(FileSystemInfo info, List<SearchHit> into)
        {
            try
            {
                if (FuzzyMatcher.TryMatch(query, info.Name, out int score))
                    into.Add(new SearchHit(FolderItem.From(info), score));
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
            }
        }

        // Try a single fast recursive enumerator over `start`'s whole subtree
        // first — as cheap as one flat call for a healthy tree. Pseudo
        // filesystems like /proc can throw mid-walk (e.g. a process exiting
        // under us); when that happens the native enumerator can't be
        // resumed, so drop that attempt's partial matches and bisect into
        // `start`'s immediate children, retrying each independently. That
        // isolates the failure to just the offending branch instead of
        // paying a per-directory cost across the whole, otherwise healthy,
        // tree. Returns false only if `start` itself can't be listed at all.
        bool Walk(string start)
        {
            if (truncated || ct.IsCancellationRequested)
                return true;

            List<SearchHit> local = [];
            int localVisited = 0;
            try
            {
                foreach (FileSystemInfo info in new DirectoryInfo(start).EnumerateFileSystemInfos("*", fastOptions))
                {
                    if (ct.IsCancellationRequested)
                        break;
                    if (visited + ++localVisited > MaxVisitedEntries)
                    {
                        truncated = true;
                        break;
                    }
                    TryAdd(info, local);
                }
                visited += localVisited;
                hits.AddRange(local);
                return true;
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
            }

            List<FileSystemInfo> children;
            try
            {
                children = [.. new DirectoryInfo(start).EnumerateFileSystemInfos("*", flatOptions)];
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                failureMessage = ex.Message;
                return false;
            }

            foreach (FileSystemInfo info in children)
            {
                if (truncated || ct.IsCancellationRequested)
                    break;
                if (++visited > MaxVisitedEntries)
                {
                    truncated = true;
                    break;
                }
                TryAdd(info, hits);
                if (info.Attributes.HasFlag(FileAttributes.Directory))
                    Walk(info.FullName);
            }
            return true;
        }

        if (!Walk(root))
            return CommandResult<SearchHit[]>.Fail("permission_denied", failureMessage!);

        SearchHit[] top = [.. hits.OrderByDescending(h => h.Score).Take(Math.Max(1, maxResults))];
        return truncated
            ? CommandResult<SearchHit[]>.Fail("truncated", "Search stopped early; results may be incomplete.", top)
            : CommandResult<SearchHit[]>.Ok(top);
    }
}
