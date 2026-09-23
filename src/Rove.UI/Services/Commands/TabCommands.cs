namespace Rove.UI.Services;

public sealed class TabCommands : ICommandTarget
{
    private readonly CommandRegistry _registry;
    private readonly Func<TabCommands?> _inFront;
    private readonly Dictionary<string, Action> _handlers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Func<bool>?> _canRuns = new(StringComparer.Ordinal);

    public TabCommands(CommandRegistry registry, Func<TabCommands?> inFront)
    {
        _registry = registry;
        _inFront = inFront;
    }

    public void Register(CommandDef def, Action method, Func<bool>? canRun = null)
    {
        string id = def.Id;
        _handlers[id] = method;
        _canRuns[id] = canRun;
        _registry.Register(def, () => RunInFront(id), () => CanRunInFront(id));
    }

    public void Unregister(string commandId)
    {
        _handlers.Remove(commandId);
        _canRuns.Remove(commandId);
        _registry.Unregister(commandId);
    }

    public IEnumerable<string> CommandIdsStartingWith(string prefix) =>
        _registry.CommandIdsStartingWith(prefix);

    private void RunInFront(string commandId)
    {
        if (_inFront() is { } front && front._handlers.TryGetValue(commandId, out Action? handler))
            handler();
    }

    private bool CanRunInFront(string commandId)
    {
        if (_inFront() is not { } front)
            return true;
        return !front._canRuns.TryGetValue(commandId, out Func<bool>? canRun) || (canRun?.Invoke() ?? true);
    }
}
