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
    private void ToggleRenameItem()
    {
        if (RefusedInArchive("Rename") || RefusedInTrash("Rename") || RefusedInAdminView("Rename"))
            return;
        if (HighlightedItem is not { } selected)
            return;
        selected.EditText = selected.Name;
        selected.IsRenaming = !selected.IsRenaming;
    }

    private void ApplyRename()
    {
        if (HighlightedItem is not { } selected)
            return;

        string newName = selected.EditText.Trim();
        if (newName.Length == 0 || newName == selected.Name)
        {
            selected.IsRenaming = false;
            return;
        }

        CommandResult<FolderItem?> result = _core.Actions.RenameItem(new(selected.Item.FullPath, newName));
        if (!result.IsOk || result.Data is null)
        {
            ErrorRaised?.Invoke(result.Message ?? "Rename failed.");
            return;
        }

        string oldPath = selected.Item.FullPath;
        string oldName = selected.Name;
        selected.IsRenaming = false;
        DirectoryListing.Rename(oldPath, result.Data);
        DirectoryListing.ListSelection.SelectPath(result.Data.FullPath);
        _undo.Push(new(UndoAction.RenameBack, $"rename of {oldName}",
            [new PathPair(oldPath, result.Data.FullPath)]));
    }

    [ObservableProperty]
    private bool _inCreateItem;

    [ObservableProperty]
    private string _createItemText = string.Empty;

    [ObservableProperty]
    private string _createItemLabel = "New file";

    private bool _createIsFolder;

    private void ToggleCreateFile() => ToggleCreate(isFolder: false);
    private void ToggleCreateFolder() => ToggleCreate(isFolder: true);

    private void ToggleCreate(bool isFolder)
    {
        if (!InCreateItem && (RefusedInArchive(isFolder ? "New folder" : "New file")
                || RefusedInTrash(isFolder ? "New folder" : "New file")
                || RefusedInAdminView(isFolder ? "New folder" : "New file")))
        {
            return;
        }
        CreateItemText = string.Empty;
        _createIsFolder = isFolder;
        CreateItemLabel = isFolder ? "New folder" : "New file";
        InCreateItem = !InCreateItem;
    }

    private void CancelCreate() => InCreateItem = false;

    private void ApplyCreate()
    {
        string name = CreateItemText.Trim();
        if (name.Length == 0)
        {
            InCreateItem = false;
            return;
        }

        CommandResult<FolderItem?> result = _core.Actions.CreateItem(
            new(DirectoryListing.CurrentDir, name, IsDirectory: _createIsFolder)
        );
        if (!result.IsOk || result.Data is null)
        {
            ErrorRaised?.Invoke(result.Message ?? "Create failed.");
            return;
        }

        InCreateItem = false;
        DirectoryListing.Upsert(result.Data);
        DirectoryListing.ListSelection.SelectPath(result.Data.FullPath);
        _undo.Push(new(UndoAction.RemoveCreated, $"new {(_createIsFolder ? "folder" : "file")} {name}",
            [new PathPair(string.Empty, result.Data.FullPath)]));
    }
}
