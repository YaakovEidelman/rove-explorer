using Avalonia.Input;
using Rove.Core.Services;
using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace Rove.UI.Services;

public static class KeymapConfig
{
    public static string DefaultPath => RovePaths.KeybindingsFile;

    private const int MaxProblemsKept = 20;

    private static readonly JsonDocumentOptions _jsonOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    public static KeymapLoad LoadDefault() => Load(DefaultPath);

    public static KeymapLoad Load(string path)
    {
        string text;
        try
        {
            if (!File.Exists(path))
                return KeymapLoad.Empty;
            text = File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new([], [$"could not be read ({ex.Message})"]);
        }

        return Parse(text);
    }

    public static KeymapLoad Parse(string json)
    {
        List<KeymapOverride> overrides = [];
        List<string> problems = [];

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json, _jsonOptions);
        }
        catch (JsonException ex)
        {
            return new([], [$"is not valid JSON ({ex.Message})"]);
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return new([], ["must be an object of command → key, e.g. { \"content.move_down\": \"ctrl+j\" }"]);

            foreach (JsonProperty entry in document.RootElement.EnumerateObject())
                ParseEntry(entry, overrides, problems);
        }

        return new(overrides, problems);
    }

    private static void ParseEntry(JsonProperty entry, List<KeymapOverride> overrides, List<string> problems)
    {
        string command = entry.Name;
        if (!IsKnownCommand(command))
        {
            Add(problems, $"unknown command \"{command}\"");
            return;
        }

        Mode[] modes;
        JsonElement keyValue;

        if (entry.Value.ValueKind == JsonValueKind.Object)
        {
            if (!entry.Value.TryGetProperty("mode", out JsonElement modeElement)
                || modeElement.ValueKind != JsonValueKind.String
                || !Enum.TryParse(modeElement.GetString(), ignoreCase: true, out Mode mode))
            {
                Add(problems, $"\"{command}\" needs a valid \"mode\", e.g. {{ \"mode\": \"browse\", \"key\": ... }}");
                return;
            }
            modes = [mode];
            keyValue = entry.Value.TryGetProperty("key", out JsonElement k) ? k : default;
        }
        else
        {
            modes = DefaultModesFor(command);
            keyValue = entry.Value;
        }

        if (!TryParseKeyValue(keyValue, out KeyStroke[]? strokes, out string? problem))
        {
            Add(problems, $"\"{command}\": {problem}");
            return;
        }

        if (modes.Length == 0)
        {
            if (strokes is not null)
                Add(problems, $"\"{command}\" has no key by default — say where with {{ \"mode\": \"browse\", \"key\": ... }}");
            return;
        }

        foreach (Mode mode in modes)
        {
            if (strokes is null)
            {
                foreach (KeyStroke stroke in DefaultStrokesFor(mode, command))
                    overrides.Add(new(mode, stroke, null));
            }
            else
            {
                foreach (KeyStroke stroke in strokes)
                    overrides.Add(new(mode, stroke, command));
            }
        }
    }

    private static bool TryParseKeyValue(JsonElement value, out KeyStroke[]? strokes, out string? problem)
    {
        strokes = null;
        problem = null;

        switch (value.ValueKind)
        {
            case JsonValueKind.Undefined:
            case JsonValueKind.Null:
                return true;

            case JsonValueKind.String:
                string text = value.GetString() ?? string.Empty;
                if (text.Length == 0)
                    return true;
                if (!TryParseStroke(text, out KeyStroke stroke))
                {
                    problem = $"unknown key \"{text}\"";
                    return false;
                }
                strokes = [stroke];
                return true;

            case JsonValueKind.Array:
                List<KeyStroke> list = [];
                foreach (JsonElement item in value.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.String || !TryParseStroke(item.GetString() ?? string.Empty, out KeyStroke itemStroke))
                    {
                        problem = $"unknown key \"{item}\"";
                        return false;
                    }
                    list.Add(itemStroke);
                }
                if (list.Count == 0)
                {
                    problem = "must list at least one key, or use null to unbind";
                    return false;
                }
                strokes = [.. list];
                return true;

            default:
                problem = "must be a key, a list of keys, or null";
                return false;
        }
    }

    private static readonly Lazy<Dictionary<string, Mode[]>> _defaultModesByCommand = new(() =>
        KeymapDefaults.DefaultModeBindings
            .SelectMany(map => map.Value.Select(ks => (Mode: map.Key, ks.Action)))
            .GroupBy(t => t.Action, t => t.Mode)
            .ToDictionary(g => g.Key, g => g.Distinct().ToArray()));

    private static Mode[] DefaultModesFor(string command) =>
        _defaultModesByCommand.Value.TryGetValue(command, out Mode[]? modes) ? modes : [];

    private static IEnumerable<KeyStroke> DefaultStrokesFor(Mode mode, string command) =>
        KeymapDefaults.DefaultModeBindings.TryGetValue(mode, out KommandShortcut[]? shortcuts)
            ? shortcuts.Where(s => s.Action == command).Select(s => s.Stroke)
            : [];

    private static void Add(List<string> problems, string problem)
    {
        if (problems.Count < MaxProblemsKept)
            problems.Add(problem);
    }

    private static readonly Lazy<HashSet<string>> _knownCommands = new(() =>
        [.. typeof(CommandDef)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(CommandDef))
            .Select(f => ((CommandDef)f.GetValue(null)!).Id),
          .. Enumerable.Range(0, BookmarkStore.ShortcutCount).Select(i => CommandDef.BookmarkGo(i).Id),
          .. Enumerable.Range(0, CommandDef.TabShortcutCount).Select(i => CommandDef.TabGo(i).Id)]);

    public static IEnumerable<string> KnownCommandIds() =>
        _knownCommands.Value.OrderBy(id => id, StringComparer.Ordinal);

    private static bool IsKnownCommand(string id) =>
        _knownCommands.Value.Contains(id)
        || id.StartsWith(CommandDef.DriveIdPrefix, StringComparison.Ordinal);

    public static bool TryParseStroke(string text, out KeyStroke stroke)
    {
        stroke = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        string[] parts = text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || text.TrimEnd().EndsWith("++", StringComparison.Ordinal))
            parts = [.. parts, "+"];

        KeyModifiers modifiers = KeyModifiers.None;
        for (int i = 0; i < parts.Length - 1; i++)
        {
            KeyModifiers? modifier = ParseModifier(parts[i]);
            if (modifier is null)
                return false;
            modifiers |= modifier.Value;
        }

        if (!TryParseKey(parts[^1], out Key key))
            return false;

        stroke = new KeyStroke(key, modifiers);
        return true;
    }

    private static KeyModifiers? ParseModifier(string part) => part.ToLowerInvariant() switch
    {
        "ctrl" or "control" => KeyModifiers.Control,
        "alt" or "option" => KeyModifiers.Alt,
        "shift" => KeyModifiers.Shift,
        "meta" or "win" or "super" or "cmd" or "command" => KeyModifiers.Meta,
        _ => null,
    };

    private static readonly Dictionary<string, Key> _keyAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["/"] = Key.OemQuestion,
        ["?"] = Key.OemQuestion,
        ["\\"] = Key.OemBackslash,
        [","] = Key.OemComma,
        ["."] = Key.OemPeriod,
        ["-"] = Key.OemMinus,
        ["="] = Key.OemPlus,
        ["+"] = Key.OemPlus,
        [";"] = Key.OemSemicolon,
        ["'"] = Key.OemQuotes,
        ["["] = Key.OemOpenBrackets,
        ["]"] = Key.OemCloseBrackets,
        ["`"] = Key.OemTilde,
        ["space"] = Key.Space,
        ["enter"] = Key.Enter,
        ["return"] = Key.Enter,
        ["esc"] = Key.Escape,
        ["escape"] = Key.Escape,
        ["backspace"] = Key.Back,
        ["back"] = Key.Back,
        ["del"] = Key.Delete,
        ["delete"] = Key.Delete,
        ["tab"] = Key.Tab,
        ["up"] = Key.Up,
        ["down"] = Key.Down,
        ["left"] = Key.Left,
        ["right"] = Key.Right,
        ["pageup"] = Key.PageUp,
        ["pagedown"] = Key.PageDown,
        ["home"] = Key.Home,
        ["end"] = Key.End,
        ["insert"] = Key.Insert,
    };

    private static bool TryParseKey(string name, out Key key)
    {
        if (_keyAliases.TryGetValue(name, out key))
            return true;
        if (name.Length == 1 && char.IsAsciiDigit(name[0]))
        {
            key = Key.D0 + (name[0] - '0');
            return true;
        }
        if (name.Length == 1 && char.IsAsciiLetter(name[0]))
        {
            key = Enum.Parse<Key>(char.ToUpperInvariant(name[0]).ToString(CultureInfo.InvariantCulture));
            return true;
        }
        return Enum.TryParse(name, ignoreCase: true, out key) && Enum.IsDefined(key);
    }
}
