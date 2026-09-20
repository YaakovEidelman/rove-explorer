namespace Rove.Core.Protocol;

public record ReadDirectoryArgs(string Path);
public record GetParentArgs(string Path);

/// <summary>A path as a person typed it, plus the folder relative paths are measured from.</summary>
public record ResolvePathArgs(string Input, string CurrentDirectory);
public record LaunchFileArgs(string Path);

/// <summary>Every installed app that takes a file to open.</summary>
public record ListOpenWithArgs();

/// <summary>Open Path with the app whose .desktop file is DesktopFile.</summary>
public record LaunchFileWithArgs(string Path, string DesktopFile);
public record GetIconArgs(FolderItem Item, int Size);

/// <summary>Rename keeps the item in place: NewName is a bare name, never a path.</summary>
public record RenameItemArgs(string Path, string NewName);

/// <summary>Move N items *into* TargetDirectory, keeping their names.</summary>
public record MoveItemsArgs(string[] Paths, string TargetDirectory, bool Overwrite);
public record CopyItemsArgs(string[] Paths, string TargetDirectory, bool Overwrite);

public record DeleteItemsArgs(string[] Paths);

/// <summary>Unpack each archive into a new folder inside TargetDirectory.</summary>
public record ExtractArchivesArgs(string[] Paths, string TargetDirectory);

/// <summary>One entry of a zip, wanted as a real file something else can open.</summary>
public record CopyOutOfArchiveArgs(string Path);

/// <summary>Pack every path into one new zip inside TargetDirectory.</summary>
public record CompressItemsArgs(string[] Paths, string TargetDirectory);

/// <summary>Bring items back out of the trash, to where they were deleted from.</summary>
public record RestoreItemsArgs(string[] Paths);

/// <summary>Open the trash: a folder to walk into on Linux, a shell window on Windows.</summary>
public record OpenTrashArgs();

/// <summary>Undoing a create: removes the item, but only while it is still empty.</summary>
public record DeleteIfEmptyArgs(string Path);

/// <summary>Name is a bare file/folder name created inside Directory.</summary>
public record CreateItemArgs(string Directory, string Name, bool IsDirectory);

public record ListDrivesArgs();

public record SearchGlobalArgs(string Root, string Query, int MaxResults);
public record GetMetadataArgs(string Path);
