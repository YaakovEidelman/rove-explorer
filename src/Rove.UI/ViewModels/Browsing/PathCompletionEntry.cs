namespace Rove.UI.ViewModels;

public record PathCompletionEntry(string Name, string FullPath, bool IsDirectory)
{
    public string Display => IsDirectory ? FullPath + Path.DirectorySeparatorChar : FullPath;
}
