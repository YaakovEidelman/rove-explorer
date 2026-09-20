using Rove.Core.Protocol;
using Rove.Core.Services;

namespace Rove.Core.Endpoints;

public partial class Actions
{
    private readonly IIconFetcher _fetcher;

    public Actions()
    {
        _fetcher = IconFetcherChooser.CreateForHost();
    }

    private static CommandResult<OpResult[]> RunBatch(
        string verb,
        string[] paths,
        IProgress<FileOpProgress>? progress,
        CancellationToken ct,
        Func<string, ProgressTicker, int, OpResult> runOne,
        Func<CommandResult<OpResult[]>?>? precondition
    )
    {
        if (precondition?.Invoke() is { } failed)
            return failed;

        ProgressTicker ticker = new(progress);
        OpResult[] results = new OpResult[paths.Length];
        bool cancelled = false;

        for (int i = 0; i < paths.Length; i++)
        {
            string name = Path.GetFileName(Path.TrimEndingDirectorySeparator(LongPath.Display(paths[i])));
            if (cancelled || ct.IsCancellationRequested)
            {
                cancelled = true;
                results[i] = OpResult.Failure(paths[i], "cancelled", $"{name} was cancelled.");
                continue;
            }
            ticker.Report(verb, i, paths.Length, name, important: true);
            results[i] = runOne(paths[i], ticker, i);
            if (results[i].Reason == "cancelled")
                cancelled = true;
        }

        ticker.Report(verb, paths.Length, paths.Length, "", important: true);
        return Summarize(results);
    }

    private static bool Exists(string path) => Directory.Exists(path) || File.Exists(path);

    private static void TryDeleteTree(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            File.Delete(LongPath.ForIo(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static CommandResult<OpResult[]> Summarize(OpResult[] results)
    {
        int failed = results.Count(r => !r.Ok);
        if (failed == 0)
            return CommandResult<OpResult[]>.Ok(results);

        string message = string.Join("; ", results.Where(r => !r.Ok).Select(r => r.Message ?? r.Reason).Distinct());
        string reason = failed == results.Length ? results[0].Reason : "partial_failure";
        return CommandResult<OpResult[]>.Fail(reason, message, results);
    }
}
