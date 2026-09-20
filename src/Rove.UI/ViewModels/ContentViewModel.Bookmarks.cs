using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rove.Core;
using Rove.Core.Protocol;
using Rove.Core.Services;
using Rove.UI.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Rove.UI.Models;

namespace Rove.UI.ViewModels;

public partial class ContentViewModel
{
    private void ToggleBookmark()
    {
        Bookmark mark = HighlightedItem is { } highlighted
            ? new(highlighted.Item.FullPath, highlighted.Item.Name, highlighted.Item.IsDirectory)
            : new(DirectoryListing.CurrentDir, FolderName(DirectoryListing.CurrentDir), true);

        bool added = _bookmarks.Toggle(mark);
        InfoRaised?.Invoke(added
            ? $"Bookmarked {mark.Name}{Shortcut(mark)}."
            : $"Removed the bookmark for {mark.Name}.");
    }

    private string Shortcut(Bookmark mark)
    {
        string key = BookmarkStore.ShortcutFor(_bookmarks.IndexOf(mark.Path));
        return key.Length == 0 ? string.Empty : $" · {key}";
    }

    private static string FolderName(string path)
    {
        string name = Path.GetFileName(Path.TrimEndingDirectorySeparator(LongPath.Display(path)));
        return name.Length > 0 ? name : LongPath.Display(path);
    }

    public void GoToBookmark(Bookmark mark)
    {
        if (mark.IsDirectory)
        {
            _ = SetCurrentDirectoryAsync(mark.Path);
            return;
        }

        string? parent = Path.GetDirectoryName(LongPath.Display(mark.Path));
        if (parent is not { Length: > 0 })
        {
            ErrorRaised?.Invoke($"{mark.Name} is not somewhere Rove can go.");
            return;
        }
        _ = SetCurrentDirectoryAsync(parent, mark.Path);
    }

    private void GoToBookmarkAt(int index)
    {
        if (_bookmarks.At(index) is not { } mark)
        {
            InfoRaised?.Invoke($"There is no bookmark {index + 1} yet.");
            return;
        }
        GoToBookmark(mark);
    }
}
