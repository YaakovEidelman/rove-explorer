using Rove.Core.Protocol;
using Rove.Core.Services;

namespace Rove.Core.Endpoints;

public partial class Actions
{
    public CommandResult<FolderItem?> RenameItem(RenameItemArgs args)
    {
        string? parent = Path.GetDirectoryName(Path.GetFullPath(LongPath.Display(args.Path)));
        if (parent is null)
            return CommandResult<FolderItem?>.Fail("invalid_path", "Cannot rename a root directory.");

        string? newPath = PathGuard.SafeCombine(parent, args.NewName, out string? reason);
        if (newPath is null)
            return CommandResult<FolderItem?>.Fail(reason!, $"Invalid name: {args.NewName}");

        string source = LongPath.ForIo(args.Path);
        string target = LongPath.ForIo(newPath);
        bool onlyCaseChanged = PathCompare.PathMatches(newPath, Path.GetFullPath(LongPath.Display(args.Path)));
        try
        {
            if (Directory.Exists(source))
            {
                if (!onlyCaseChanged && Exists(target))
                    return CommandResult<FolderItem?>.Fail("already_exists", $"Something named {args.NewName} already exists here.");
                Directory.Move(source, target);
                return CommandResult<FolderItem?>.Ok(FolderItem.From(new DirectoryInfo(target)));
            }
            if (File.Exists(source))
            {
                if (!onlyCaseChanged && Exists(target))
                    return CommandResult<FolderItem?>.Fail("already_exists", $"Something named {args.NewName} already exists here.");
                File.Move(source, target);
                return CommandResult<FolderItem?>.Ok(FolderItem.From(new FileInfo(target)));
            }
            return CommandResult<FolderItem?>.Fail("not_found", $"No such item: {args.Path}");
        }
        catch (UnauthorizedAccessException)
        {
            return CommandResult<FolderItem?>.Fail("permission_denied", $"Access denied: {args.Path}");
        }
        catch (PathTooLongException ex)
        {
            return CommandResult<FolderItem?>.Fail("path_too_long", ex.Message);
        }
        catch (IOException ex)
        {
            return CommandResult<FolderItem?>.Fail("io_error", ex.Message);
        }
    }

    public CommandResult<FolderItem?> CreateItem(CreateItemArgs args)
    {
        string? path = PathGuard.SafeCombine(args.Directory, args.Name, out string? reason);
        if (path is null)
            return CommandResult<FolderItem?>.Fail(reason!, $"Invalid name: {args.Name}");

        string io = LongPath.ForIo(path);
        try
        {
            if (Exists(io))
                return CommandResult<FolderItem?>.Fail("already_exists", $"{args.Name} already exists.");

            if (args.IsDirectory)
            {
                DirectoryInfo dir = Directory.CreateDirectory(io);
                return CommandResult<FolderItem?>.Ok(FolderItem.From(dir));
            }
            using (new FileStream(io, FileMode.CreateNew)) { }
            return CommandResult<FolderItem?>.Ok(FolderItem.From(new FileInfo(io)));
        }
        catch (UnauthorizedAccessException)
        {
            return CommandResult<FolderItem?>.Fail("permission_denied", $"Access denied: {path}");
        }
        catch (PathTooLongException ex)
        {
            return CommandResult<FolderItem?>.Fail("path_too_long", ex.Message);
        }
        catch (IOException ex)
        {
            return CommandResult<FolderItem?>.Fail("io_error", ex.Message);
        }
    }
}
