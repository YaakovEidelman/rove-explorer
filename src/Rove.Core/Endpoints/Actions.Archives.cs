using Rove.Core.Protocol;
using Rove.Core.Services;

namespace Rove.Core.Endpoints;

public partial class Actions
{
    public CommandResult<OpResult[]> ExtractArchives(ExtractArchivesArgs args) =>
        ExtractArchives(args, null, CancellationToken.None);

    public Task<CommandResult<OpResult[]>> ExtractArchivesAsync(
        ExtractArchivesArgs args,
        IProgress<FileOpProgress>? progress = null,
        CancellationToken ct = default
    ) => Task.Run(() => ExtractArchives(args, progress, ct), CancellationToken.None);

    private static CommandResult<OpResult[]> ExtractArchives(
        ExtractArchivesArgs args, IProgress<FileOpProgress>? progress, CancellationToken ct
    ) => RunBatch("Extracting", args.Paths, progress, ct,
            (source, ticker, index) => ExtractOne(source, args.TargetDirectory, ticker, index, ct),
            () => Directory.Exists(LongPath.ForIo(args.TargetDirectory))
                ? null
                : CommandResult<OpResult[]>.Fail("not_found", $"Target directory does not exist: {args.TargetDirectory}"));

    private static OpResult ExtractOne(
        string archivePath, string targetDir, ProgressTicker ticker, int index, CancellationToken ct
    )
    {
        string name = Path.GetFileName(Path.TrimEndingDirectorySeparator(LongPath.Display(archivePath)));
        string source = LongPath.ForIo(archivePath);

        if (!File.Exists(source))
            return OpResult.Failure(archivePath, "not_found", $"No such file: {archivePath}");
        if (!ArchiveService.IsArchive(archivePath))
            return OpResult.Failure(archivePath, "not_an_archive", $"{name} is not a zip file.");
        if (ArchiveService.FreeDestination(targetDir, archivePath) is not { } destination)
            return OpResult.Failure(archivePath, "already_exists", $"There is no free name left for {name} here.");

        try
        {
            ct.ThrowIfCancellationRequested();
            ArchiveService.Extract(archivePath, destination, "Extracting", ticker, index, ct);
            return OpResult.Success(archivePath, FolderItem.From(new DirectoryInfo(LongPath.ForIo(destination))));
        }
        catch (OperationCanceledException)
        {
            TryDeleteTree(LongPath.ForIo(destination));
            return OpResult.Failure(archivePath, "cancelled", $"{name} was cancelled.");
        }
        catch (InvalidDataException)
        {
            TryDeleteTree(LongPath.ForIo(destination));
            return OpResult.Failure(archivePath, "bad_archive", $"{name} is damaged, or is not really a zip file.");
        }
        catch (UnauthorizedAccessException)
        {
            TryDeleteTree(LongPath.ForIo(destination));
            return OpResult.Failure(archivePath, "permission_denied", $"Access denied while unpacking {name}.");
        }
        catch (PathTooLongException ex)
        {
            TryDeleteTree(LongPath.ForIo(destination));
            return OpResult.Failure(archivePath, "path_too_long", ex.Message);
        }
        catch (IOException ex)
        {
            TryDeleteTree(LongPath.ForIo(destination));
            return OpResult.Failure(archivePath, "io_error", ex.Message);
        }
    }

    public CommandResult<OpResult[]> CompressItems(CompressItemsArgs args) =>
        CompressItems(args, null, CancellationToken.None);

    public Task<CommandResult<OpResult[]>> CompressItemsAsync(
        CompressItemsArgs args,
        IProgress<FileOpProgress>? progress = null,
        CancellationToken ct = default
    ) => Task.Run(() => CompressItems(args, progress, ct), CancellationToken.None);

    private static CommandResult<OpResult[]> CompressItems(
        CompressItemsArgs args, IProgress<FileOpProgress>? progress, CancellationToken ct
    )
    {
        if (args.Paths.Length == 0)
            return CommandResult<OpResult[]>.Fail("nothing_to_do", "There is nothing here to compress.");
        if (!Directory.Exists(LongPath.ForIo(args.TargetDirectory)))
            return CommandResult<OpResult[]>.Fail("not_found", $"Target directory does not exist: {args.TargetDirectory}");

        string stem = ArchiveService.ArchiveStem(args.Paths, args.TargetDirectory);
        if (ArchiveService.FreeArchive(args.TargetDirectory, stem) is not { } destination)
            return CommandResult<OpResult[]>.Fail(
                "already_exists", $"There is no free name left for {stem}{ArchiveService.ArchiveExtension} here.");

        string name = Path.GetFileName(destination);
        ProgressTicker ticker = new(progress);
        ticker.Report("Compressing", 0, args.Paths.Length, name, important: true);

        try
        {
            ArchiveService.Compress(args.Paths, destination, "Compressing", ticker, ct);
            ticker.Report("Compressing", args.Paths.Length, args.Paths.Length, "", important: true);
            return Summarize([OpResult.Success(
                destination, FolderItem.From(new FileInfo(LongPath.ForIo(destination))))]);
        }
        catch (OperationCanceledException)
        {
            TryDeleteFile(destination);
            return Summarize([OpResult.Failure(destination, "cancelled", $"{name} was cancelled.")]);
        }
        catch (FileNotFoundException ex)
        {
            TryDeleteFile(destination);
            return Summarize([OpResult.Failure(destination, "not_found", ex.Message)]);
        }
        catch (UnauthorizedAccessException)
        {
            TryDeleteFile(destination);
            return Summarize([OpResult.Failure(destination, "permission_denied", $"Access denied while making {name}.")]);
        }
        catch (PathTooLongException ex)
        {
            TryDeleteFile(destination);
            return Summarize([OpResult.Failure(destination, "path_too_long", ex.Message)]);
        }
        catch (IOException ex)
        {
            TryDeleteFile(destination);
            return Summarize([OpResult.Failure(destination, "io_error", ex.Message)]);
        }
    }
}
