using Rove.Core.Services;

namespace Rove.UI.Services;

public class CommandRegistry : ICommandTarget
{
    private readonly Dictionary<Mode, Dictionary<KeyStroke, string>> _modeBindings = [];
    private readonly Dictionary<string, Command> _commands = [];

    public CommandRegistry() : this(KeymapLoad.Empty)
    {
    }

    public CommandRegistry(KeymapLoad keymap)
    {
        foreach (Mode mode in Enum.GetValues<Mode>())
            _modeBindings.Add(mode, []);

        HashSet<(Mode Mode, string Action)> remapped =
            [.. keymap.Overrides
                .Where(o => o.Action is { Length: > 0 })
                .Select(o => (o.Mode, o.Action!))];

        foreach (KeyValuePair<Mode, KommandShortcut[]> map in KeymapDefaults.DefaultModeBindings)
        {
            foreach (KommandShortcut ks in map.Value)
            {
                if (!remapped.Contains((map.Key, ks.Action)))
                    Bind(map.Key, ks);
            }
        }

        foreach (KeymapOverride entry in keymap.Overrides)
            Rebind(entry);
    }

    public void Bind(Mode mode, KommandShortcut ks) => _modeBindings[mode].TryAdd(ks.Stroke, ks.Action);

    public void Rebind(KeymapOverride entry)
    {
        if (entry.Action is { Length: > 0 } action)
            _modeBindings[entry.Mode][entry.Stroke] = action;
        else
            _modeBindings[entry.Mode].Remove(entry.Stroke);
    }

    public void Register(CommandDef def, Action method) => _commands[def.Id] = new(def, method);

    public void Unregister(string commandId) => _commands.Remove(commandId);

    public IEnumerable<string> CommandIdsStartingWith(string prefix) =>
        [.. _commands.Keys.Where(id => id.StartsWith(prefix, StringComparison.Ordinal))];

    public bool TryExecute(Mode mode, KeyStroke stroke)
    {
        if (!_modeBindings.TryGetValue(mode, out Dictionary<KeyStroke, string>? binding)) return false;
        if (!binding.TryGetValue(stroke, out string? action)) return false;
        return TryExecute(action);
    }

    public bool TryExecute(string action)
    {
        if (!_commands.TryGetValue(action, out Command command)) return false;
        command.Method();
        return true;
    }

    public string HintFor(string commandId)
    {
        foreach (Mode mode in Enum.GetValues<Mode>())
        {
            foreach (KeyValuePair<KeyStroke, string> pair in _modeBindings[mode])
            {
                if (pair.Value == commandId)
                    return pair.Key.Display();
            }
        }
        return "";
    }

    public Command[] Commands() =>
        [.. _commands.Values
            .Where(c => c.Def.CommandKind == CommandKind.User)
            .OrderBy(c => c.Def.Order)
            .ThenBy(c => c.Def.Title, StringComparer.OrdinalIgnoreCase)];

    public Command[] FilteredCommands(string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
            return Commands();

        return [.. _commands.Values
            .Where(c => c.Def.CommandKind == CommandKind.User)
            .Select(c => (Command: c, Matched: FuzzyMatcher.TryMatch(filter, c.Def.Title, out int score), Score: score))
            .Where(t => t.Matched)
            .OrderByDescending(t => t.Score)
            .ThenBy(t => t.Command.Def.Order)
            .ThenBy(t => t.Command.Def.Title, StringComparer.OrdinalIgnoreCase)
            .Select(t => t.Command)];
    }
}
