using Rove.Core.Protocol;
using Rove.Core.Services;

namespace Rove.UI.ViewModels;

public partial class DirectoryListing
{
    private int GetIndexFromContent(string path) =>
        _unfilteredContent.FindIndex(x => PathCompare.PathMatches(x.Item.FullPath, path));

    private bool Belongs(string path) =>
        Path.GetDirectoryName(LongPath.Display(path)) is { Length: > 0 } parent
        && PathCompare.PathMatches(parent, CurrentDir);

    public void Upsert(FolderItem item)
    {
        if (!Belongs(item.FullPath))
            return;
        MutateUpsert(item);
        SortContent();
        ApplyView();
    }

    private void MutateUpsert(FolderItem item) => MutateUpsert(GetIndexFromContent(item.FullPath), item);

    private void MutateUpsert(int index, FolderItem item)
    {
        if (!IsKept(item))
        {
            RemoveAt(index);
            return;
        }
        if (index != -1)
        {
            ListViewItem row = _unfilteredContent[index];
            if (!string.Equals(row.Name, item.Name, StringComparison.Ordinal)
                || IsHidden(row.Item) != IsHidden(item))
            {
                InvalidateFilter();
            }
            row.UpdateData(item);
        }
        else
        {
            _unfilteredContent.Add(new(item, _iconSize, _cache));
            InvalidateFilter();
        }
    }

    public void Rename(string oldPath, FolderItem item)
    {
        if (!Belongs(item.FullPath))
        {
            RemoveWithApply(oldPath);
            return;
        }
        MutateRename(oldPath, item);
        SortContent();
        ApplyView();
    }

    private void MutateRename(string oldPath, FolderItem item)
    {
        int existingIndex = GetIndexFromContent(oldPath);
        if (existingIndex == -1)
            existingIndex = GetIndexFromContent(item.FullPath);
        MutateUpsert(existingIndex, item);
        InvalidateFilter();
    }

    public void Remove(string path) => RemoveAt(GetIndexFromContent(path));

    public void RemoveWithApply(string path)
    {
        Remove(path);
        ApplyView();
    }

    private void RemoveAt(int index)
    {
        if (index == -1)
            return;
        _unfilteredContent.RemoveAt(index);
        InvalidateFilter();
    }

    public void ApplyBatch(IReadOnlyList<WatchEvent> events)
    {
        foreach (WatchEvent e in events)
        {
            switch (e)
            {
                case WatchEvent.Upserted(FolderItem item):
                    if (Belongs(item.FullPath))
                        MutateUpsert(item);
                    break;
                case WatchEvent.Deleted(string path):
                    Remove(path);
                    break;
                case WatchEvent.Renamed(string oldPath, FolderItem item):
                    if (Belongs(item.FullPath))
                        MutateRename(oldPath, item);
                    else
                        Remove(oldPath);
                    break;
            }
        }
        SortContent();
        ApplyView();
    }
}
