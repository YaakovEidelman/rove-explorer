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
    [ObservableProperty]
    private bool _inEditPath;

    [ObservableProperty]
    private string _editPathText = string.Empty;

    [ObservableProperty]
    private int _editPathCaret;

    private bool _completionIsWriting;

    partial void OnEditPathTextChanged(string value)
    {
        if (_completionIsWriting)
            return;
        _ = Completions.NarrowAsync(value, DirectoryListing.CurrentDir);
    }

    partial void OnInEditPathChanged(bool value)
    {
        if (!value)
            Completions.Close();
    }

    private void ToggleEditPath()
    {
        if (InEditPath)
        {
            InEditPath = false;
            return;
        }
        SetEditPathText(DirectoryListing.CurrentDir);
        InEditPath = true;
    }

    private void CancelEditPath() => InEditPath = false;

    private void CompletePath() => _ = CompletePathAsync();

    private async Task CompletePathAsync()
    {
        if (await Completions.ExpandAsync(EditPathText, DirectoryListing.CurrentDir) is { } completed)
            SetEditPathText(completed);
    }

    private void DismissCompletions() => Completions.Close();

    private void SetEditPathText(string text)
    {
        _completionIsWriting = true;
        try
        {
            EditPathText = text;
            EditPathCaret = text.Length;

            OnPropertyChanged(nameof(EditPathCaret));
        }
        finally
        {
            _completionIsWriting = false;
        }
    }

    private void ApplyEditPath() => _ = ApplyEditPathAsync();

    private async Task ApplyEditPathAsync()
    {
        string typed = EditPathText;
        if (typed.Trim().Length == 0)
        {
            InEditPath = false;
            return;
        }

        CommandResult<FolderItem?> found = _core.Actions.ResolvePath(new(typed, DirectoryListing.CurrentDir));
        if (!found.IsOk || found.Data is null)
        {
            ErrorRaised?.Invoke(found.Message ?? $"Could not go to {typed}.");
            return;
        }

        InEditPath = false;
        FolderItem item = found.Data;
        if (item.IsDirectory)
        {
            await SetCurrentDirectoryAsync(item.FullPath);
            return;
        }

        if (Path.GetDirectoryName(item.FullPath) is not { Length: > 0 } parent)
        {
            ErrorRaised?.Invoke($"Could not go to {item.FullPath}.");
            return;
        }
        await SetCurrentDirectoryAsync(parent, highlightPath: item.FullPath);
    }
}
