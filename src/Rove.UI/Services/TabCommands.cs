using System;
using System.Collections.Generic;

namespace Rove.UI.Services;

/// <summary>
/// Where a folder view puts the commands it answers for. With one view there
/// was only one place they could go, and it registered straight into the
/// keymap. With tabs there are several views wanting the same command ids, so
/// each keeps its own and something above decides which one a key reaches.
/// </summary>
public interface ICommandTarget
{
    void Register(CommandDef def, Action method);

    void Unregister(string commandId);

    IEnumerable<string> CommandIdsStartingWith(string prefix);
}

/// <summary>
/// One tab's commands. Each tab registers the same ids — <c>content.move_up</c>
/// and the rest — and each keeps its own handler for them. What goes into the
/// keymap is not any tab's handler but a stand-in that asks who is in front
/// and calls theirs, so <c>j</c> moves the cursor in the tab you are looking
/// at and never in one behind it.
///
/// <para>
/// Registering the same id from a second tab is not a clash: every stand-in
/// does the same thing, so whichever one the keymap ends up holding is the
/// right one.
/// </para>
/// </summary>
public sealed class TabCommands : ICommandTarget
{
    private readonly CommandRegistry _registry;
    private readonly Func<TabCommands?> _inFront;
    private readonly Dictionary<string, Action> _handlers = new(StringComparer.Ordinal);

    public TabCommands(CommandRegistry registry, Func<TabCommands?> inFront)
    {
        _registry = registry;
        _inFront = inFront;
    }

    public void Register(CommandDef def, Action method)
    {
        string id = def.Id;
        _handlers[id] = method;
        _registry.Register(def, () => RunInFront(id));
    }

    /// <summary>
    /// Drops a command this tab answered for. The ids that come and go are
    /// the ones standing for a drive, and a drive is gone for every tab at
    /// once, so the stand-in goes from the keymap too.
    /// </summary>
    public void Unregister(string commandId)
    {
        _handlers.Remove(commandId);
        _registry.Unregister(commandId);
    }

    public IEnumerable<string> CommandIdsStartingWith(string prefix) =>
        _registry.CommandIdsStartingWith(prefix);

    /// <summary>
    /// Hands the command to whichever tab is showing. A tab that never
    /// registered this id simply does nothing with it, which is the right
    /// answer for a command that belonged to a tab that has since closed.
    /// </summary>
    private void RunInFront(string commandId)
    {
        if (_inFront() is { } front && front._handlers.TryGetValue(commandId, out Action? handler))
            handler();
    }
}
