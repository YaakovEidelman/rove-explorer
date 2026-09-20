using Rove.UI.Services;
using Xunit;

namespace Rove.UI.Tests;

public class UndoStackTests
{
    private static UndoStep Step(string description) =>
        new(UndoAction.RemoveCreated, description, [new PathPair(string.Empty, $"C:\\tmp\\{description}")]);

    [Fact]
    public void TheNewestActionIsTheOneThatComesBack()
    {
        UndoStack stack = new();
        stack.Push(Step("first"));
        stack.Push(Step("second"));

        Assert.Equal("second", stack.Pop()!.Description);
        Assert.Equal("first", stack.Pop()!.Description);
        Assert.Null(stack.Pop());
    }

    [Fact]
    public void AnEmptyStackHasNothingToUndo()
    {
        UndoStack stack = new();

        Assert.False(stack.HasSteps);
        Assert.Null(stack.Peek());
        Assert.Null(stack.Pop());
    }

    [Fact]
    public void PeekingLeavesTheStepWhereItIs()
    {
        UndoStack stack = new();
        stack.Push(Step("only"));

        Assert.Equal("only", stack.Peek()!.Description);
        Assert.Equal(1, stack.Count);
    }

    [Fact]
    public void AnActionThatTouchedNothingIsNotWorthRemembering()
    {
        UndoStack stack = new();
        stack.Push(new(UndoAction.MoveBack, "paste of 0 items", []));

        Assert.False(stack.HasSteps);
    }

    [Fact]
    public void OnlyTheLastFewActionsAreKept()
    {
        UndoStack stack = new();
        for (int i = 0; i <= UndoStack.MaxDepth; i++)
            stack.Push(Step($"action {i}"));

        Assert.Equal(UndoStack.MaxDepth, stack.Count);
        Assert.Equal($"action {UndoStack.MaxDepth}", stack.Peek()!.Description);
    }
}
