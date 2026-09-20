using System;
using System.Collections.Generic;

namespace Rove.UI.Services;

public interface ICommandTarget
{
    void Register(CommandDef def, Action method);

    void Unregister(string commandId);

    IEnumerable<string> CommandIdsStartingWith(string prefix);
}

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

    public void Unregister(string commandId)
    {
        _handlers.Remove(commandId);
        _registry.Unregister(commandId);
    }

    public IEnumerable<string> CommandIdsStartingWith(string prefix) =>
        _registry.CommandIdsStartingWith(prefix);

    private void RunInFront(string commandId)
    {
        if (_inFront() is { } front && front._handlers.TryGetValue(commandId, out Action? handler))
            handler();
    }
}
