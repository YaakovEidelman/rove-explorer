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
    // ── the path bar ─────────────────────────────────────────────────────
    // The path at the top is also the way in: it opens as a text box holding
    // where you are, and takes anything a shell would take — an absolute
    // path, a relative one, ~, or an environment variable. A path naming a
    // file opens the folder around it with that file highlighted.

    [ObservableProperty]
    private bool _inEditPath;

    [ObservableProperty]
    private string _editPathText = string.Empty;

    /// <summary>
    /// Where the cursor sits in the path bar. Bound so that text put there by
    /// a completion leaves the cursor after it, ready to keep typing.
    /// </summary>
    [ObservableProperty]
    private int _editPathCaret;

    /// <summary>
    /// Set while a completion is writing the path bar, so its own write is
    /// not mistaken for the user typing and used to narrow the list again.
    /// </summary>
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

    // ── completing what is typed there ───────────────────────────────────
    // Tab finishes the name against what is really in the folder. Where more
    // than one thing matches it carries the text as far as they agree and
    // drops a list out, which is Mode.PathCompletion until it is taken or
    // dismissed. Every one of these writes the box through SetEditPathText,
    // so the list is never re-narrowed by a change it made itself.

    private void CompletePath() => _ = CompletePathAsync();

    private async Task CompletePathAsync()
    {
        if (await Completions.ExpandAsync(EditPathText, DirectoryListing.CurrentDir) is { } completed)
            SetEditPathText(completed);
    }

    private void AcceptCompletion()
    {
        if (Completions.AcceptSelected(EditPathText) is { } taken)
            SetEditPathText(taken);
    }

    private void DismissCompletions() => Completions.Close();

    /// <summary>Puts text in the path bar with the cursor left at the end of it.</summary>
    private void SetEditPathText(string text)
    {
        _completionIsWriting = true;
        try
        {
            EditPathText = text;
            EditPathCaret = text.Length;

            // Said again in case it did not change: the box moves its own
            // cursor as the user clicks around, and a value that matches the
            // one already held here would otherwise never be pushed back.
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
            // Leave the box open, with the text in it, so it can be corrected.
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
