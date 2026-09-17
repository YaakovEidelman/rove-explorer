using System.Collections.Generic;

namespace Rove.UI.Services;

/// <summary>What has to happen to put one finished action back the way it was.</summary>
public enum UndoAction
{
    /// <summary>Rename the item back to the name it had.</summary>
    RenameBack,

    /// <summary>Remove the file or folder the create verb made.</summary>
    RemoveCreated,

    /// <summary>Remove the copies a paste dropped at the destination.</summary>
    RemoveCopies,

    /// <summary>Move the items back to the folder they came from.</summary>
    MoveBack,

    /// <summary>Bring the items back out of the Recycle Bin, or the trash on Linux.</summary>
    RestoreFromTrash,
}

/// <summary>
/// Where one item was before an action and where it ended up after it.
/// Actions that only create (<see cref="UndoAction.RemoveCreated"/>) leave
/// <see cref="Before"/> empty; ones that only remove leave <see cref="After"/> empty.
/// </summary>
public readonly record struct PathPair(string Before, string After);

/// <summary>One finished action, plus everything needed to take it back.</summary>
public sealed record UndoStep(UndoAction Action, string Description, IReadOnlyList<PathPair> Items);

/// <summary>
/// The recent actions that can still be taken back, newest first. Kept in the
/// UI layer on purpose: the backend verbs stay stateless, and an undo is just
/// the opposite verb run against the paths recorded here.
/// </summary>
public sealed class UndoStack
{
    /// <summary>Deep enough to cover a run of mistakes, shallow enough to stay honest.</summary>
    public const int MaxDepth = 20;

    private readonly List<UndoStep> _steps = [];

    public bool HasSteps => _steps.Count > 0;

    public int Count => _steps.Count;

    public void Push(UndoStep step)
    {
        if (step.Items.Count == 0)
            return;
        _steps.Add(step);
        if (_steps.Count > MaxDepth)
            _steps.RemoveAt(0);
    }

    public UndoStep? Peek() => _steps.Count == 0 ? null : _steps[^1];

    public UndoStep? Pop()
    {
        if (_steps.Count == 0)
            return null;
        UndoStep step = _steps[^1];
        _steps.RemoveAt(_steps.Count - 1);
        return step;
    }

    public void Clear() => _steps.Clear();
}
