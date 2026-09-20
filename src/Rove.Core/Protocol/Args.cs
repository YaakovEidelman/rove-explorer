namespace Rove.Core.Protocol;

public record ReadDirectoryArgs(string Path);
public record GetParentArgs(string Path);

public record ResolvePathArgs(string Input, string CurrentDirectory);
public record LaunchFileArgs(string Path);

public record ListOpenWithArgs(string Path, bool All);

public record LaunchFileWithArgs(string Path, string DesktopFile);
public record GetIconArgs(FolderItem Item, int Size);

public record RenameItemArgs(string Path, string NewName);

public record MoveItemsArgs(string[] Paths, string TargetDirectory, bool Overwrite);
public record CopyItemsArgs(string[] Paths, string TargetDirectory, bool Overwrite);

public record DeleteItemsArgs(string[] Paths);

public record ExtractArchivesArgs(string[] Paths, string TargetDirectory);

public record CopyOutOfArchiveArgs(string Path);

public record CompressItemsArgs(string[] Paths, string TargetDirectory);

public record RestoreItemsArgs(string[] Paths);

public record OpenTrashArgs();

public record DeleteIfEmptyArgs(string Path);

public record CreateItemArgs(string Directory, string Name, bool IsDirectory);

public record ListDrivesArgs();

public record SearchGlobalArgs(string Root, string Query, int MaxResults);
public record GetMetadataArgs(string Path);
