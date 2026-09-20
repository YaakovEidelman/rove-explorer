using System.Collections.Generic;

namespace Rove.UI.Services;

public enum UndoAction
{
    RenameBack,

    RemoveCreated,

    RemoveCopies,

    MoveBack,

    RestoreFromTrash,
}

public readonly record struct PathPair(string Before, string After);

public sealed record UndoStep(UndoAction Action, string Description, IReadOnlyList<PathPair> Items);

public sealed class UndoStack
{
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
