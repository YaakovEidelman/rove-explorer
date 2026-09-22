using Rove.Core.Protocol;
using Rove.Core.Services;

namespace Rove.Core.Endpoints;

public partial class Actions
{
    public CommandResult<string?> CopyOutOfArchive(CopyOutOfArchiveArgs args)
    {
        if (!ArchivePath.TryParse(args.Path, out ArchivePath inside) || inside.IsRoot)
            return CommandResult<string?>.Fail("not_in_archive", $"{args.Path} is not inside a zip.");

        try
        {
            return CommandResult<string?>.Ok(ArchiveBrowser.CopyOut(inside));
        }
        catch (FileNotFoundException)
        {
            return CommandResult<string?>.Fail("not_found", $"{inside.Name} is not in that zip any more.");
        }
        catch (InvalidDataException)
        {
            return CommandResult<string?>.Fail(
                "bad_archive", $"{Path.GetFileName(inside.Archive)} is damaged, or is not really a zip file.");
        }
        catch (UnauthorizedAccessException)
        {
            return CommandResult<string?>.Fail("permission_denied", $"Access denied while reading {inside.Name}.");
        }
        catch (PathTooLongException ex)
        {
            return CommandResult<string?>.Fail("path_too_long", ex.Message);
        }
        catch (IOException ex)
        {
            return CommandResult<string?>.Fail("io_error", ex.Message);
        }
    }

    public Task<CommandResult<string?>> CopyOutOfArchiveAsync(CopyOutOfArchiveArgs args) =>
        Task.Run(() => CopyOutOfArchive(args));

    public CommandResult<string?> LaunchFile(LaunchFileArgs args)
    {
        if (ArchivePath.IsInside(args.Path))
        {
            return CommandResult<string?>.Fail(
                "in_archive", "A file inside a zip has to be taken out of it before anything can open it.");
        }

        string? error = FileOpener.Start(args.Path, out FileOpener.Opener? opener);
        opener?.Dispose();
        return error is null
            ? CommandResult<string?>.Ok(null)
            : CommandResult<string?>.Fail("launch_failed", error);
    }

    public async Task<CommandResult<string?>> LaunchFileAsync(
        LaunchFileArgs args, CancellationToken ct = default
    )
    {
        string? error = FileOpener.Start(args.Path, out FileOpener.Opener? opener);
        if (error is not null)
        {
            opener?.Dispose();
            return CommandResult<string?>.Fail("launch_failed", error);
        }

        using (opener)
        {
            error = await FileOpener.WaitForRefusal(opener, args.Path, ct);
        }
        string reason = FileOpener.IsNativeExecutable(args.Path) ? "launch_failed" : "launch_refused";
        return error is null
            ? CommandResult<string?>.Ok(null)
            : CommandResult<string?>.Fail(reason, error);
    }

    public CommandResult<string?> OpenTerminal(OpenTerminalArgs args)
    {
        if (ArchivePath.IsInside(args.Path))
        {
            return CommandResult<string?>.Fail(
                "in_archive", "A terminal cannot open inside a zip. Extract it first.");
        }

        string? error = TerminalLauncher.Start(args.Path);
        return error is null
            ? CommandResult<string?>.Ok(null)
            : CommandResult<string?>.Fail("launch_failed", error);
    }

    public CommandResult<string?> OpenWithSystemDialog(LaunchFileArgs args)
    {
        if (!OperatingSystem.IsWindows())
            return CommandResult<string?>.Fail("unsupported", "This system does not have its own Open With dialog.");

        string? error = WindowsOpenWith.Start(args.Path);
        return error is null
            ? CommandResult<string?>.Ok(null)
            : CommandResult<string?>.Fail("launch_failed", error);
    }

    public CommandResult<AppEntry[]> ListOpenWithApps(ListOpenWithArgs args)
    {
        if (!OperatingSystem.IsLinux())
        {
            return CommandResult<AppEntry[]>.Fail(
                "unsupported", "Windows asks which app to use on its own.");
        }

        try
        {
            return CommandResult<AppEntry[]>.Ok(args.All ? LinuxDesktopApps.Installed() : LinuxDesktopApps.ForFile(args.Path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return CommandResult<AppEntry[]>.Fail("io_error", ex.Message);
        }
    }

    public async Task<CommandResult<string?>> LaunchFileWithAsync(
        LaunchFileWithArgs args, CancellationToken ct = default
    )
    {
        string? error = FileOpener.StartWith(args.DesktopFile, args.Path, out FileOpener.Opener? opener);
        if (error is not null)
        {
            opener?.Dispose();
            return CommandResult<string?>.Fail("launch_failed", error);
        }

        using (opener)
        {
            error = await FileOpener.WaitForRefusal(opener, args.Path, ct);
        }
        return error is null
            ? CommandResult<string?>.Ok(null)
            : CommandResult<string?>.Fail("launch_refused", error);
    }

    public async Task<CommandResult<byte[]?>> GetItemIcon(GetIconArgs args)
    {
        try
        {
            byte[]? icon = await _fetcher.GetIconAsync(args.Item, args.Size);
            return CommandResult<byte[]?>.Ok(icon);
        }
        catch (Exception)
        {
            return CommandResult<byte[]?>.Fail("icon_error", null);
        }
    }
}
