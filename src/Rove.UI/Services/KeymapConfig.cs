using Avalonia.Input;
using Rove.Core.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace Rove.UI.Services;

/// <summary>One line of the user's keybindings file. A null action unbinds the key.</summary>
public readonly record struct KeymapOverride(Mode Mode, KeyStroke Stroke, string? Action);

/// <summary>What a load attempt produced: the overrides plus anything unusable.</summary>
public sealed record KeymapLoad(IReadOnlyList<KeymapOverride> Overrides, IReadOnlyList<string> Problems)
{
    public static KeymapLoad Empty { get; } = new([], []);

    /// <summary>One short line for the status bar, or null when the file was clean.</summary>
    public string? Summary => Problems.Count == 0
        ? null
        : $"keybindings.json: {Problems[0]}" + (Problems.Count > 1 ? $" (+{Problems.Count - 1} more)" : "");
}

/// <summary>
/// Reads keybinding overrides from the user's config file. The file is
/// optional: when it isn't there, or can't be read, the built-in defaults
/// stand on their own.
///
/// <para>
/// Shape — one object per mode, mapping a key to a command id:
/// <code>
/// { "browse": { "ctrl+j": "content.move_down", "d": null } }
/// </code>
/// A key that isn't listed keeps its default. A null (or empty) action
/// removes the default binding for that key.
/// </para>
/// </summary>
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
                return new([], ["must be an object of modes, e.g. { \"browse\": { \"j\": \"content.move_down\" } }"]);

            foreach (JsonProperty modeEntry in document.RootElement.EnumerateObject())
            {
                if (!Enum.TryParse(modeEntry.Name, ignoreCase: true, out Mode mode))
                {
                    Add(problems, $"unknown mode \"{modeEntry.Name}\"");
                    continue;
                }
                if (modeEntry.Value.ValueKind != JsonValueKind.Object)
                {
                    Add(problems, $"\"{modeEntry.Name}\" must be an object of key → command");
                    continue;
                }

                foreach (JsonProperty binding in modeEntry.Value.EnumerateObject())
                {
                    if (!TryParseStroke(binding.Name, out KeyStroke stroke))
                    {
                        Add(problems, $"unknown key \"{binding.Name}\" in {modeEntry.Name}");
                        continue;
                    }

                    if (binding.Value.ValueKind is JsonValueKind.Null)
                    {
                        overrides.Add(new(mode, stroke, null));
                        continue;
                    }
                    if (binding.Value.ValueKind != JsonValueKind.String)
                    {
                        Add(problems, $"\"{binding.Name}\" in {modeEntry.Name} must be a command id or null");
                        continue;
                    }

                    string action = binding.Value.GetString() ?? string.Empty;
                    if (action.Length == 0)
                    {
                        overrides.Add(new(mode, stroke, null));
                        continue;
                    }
                    if (!IsKnownCommand(action))
                    {
                        Add(problems, $"unknown command \"{action}\" in {modeEntry.Name}");
                        continue;
                    }
                    overrides.Add(new(mode, stroke, action));
                }
            }
        }

        return new(overrides, problems);
    }

    private static void Add(List<string> problems, string problem)
    {
        if (problems.Count < MaxProblemsKept)
            problems.Add(problem);
    }

    // ── command ids ──────────────────────────────────────────────────────

    private static readonly Lazy<HashSet<string>> _knownCommands = new(() =>
        [.. typeof(CommandDef)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(CommandDef))
            .Select(f => ((CommandDef)f.GetValue(null)!).Id),
          // The bookmark and tab shortcuts are made rather than declared — one
          // id per slot, with which bookmark or tab a slot leads to left
          // until it is pressed.
          .. Enumerable.Range(0, BookmarkStore.ShortcutCount).Select(i => CommandDef.BookmarkGo(i).Id),
          .. Enumerable.Range(0, CommandDef.TabShortcutCount).Select(i => CommandDef.TabGo(i).Id)]);

    /// <summary>Every id the app defines, sorted — the list a config file can use.</summary>
    public static IEnumerable<string> KnownCommandIds() =>
        _knownCommands.Value.OrderBy(id => id, StringComparer.Ordinal);

    private static bool IsKnownCommand(string id) =>
        _knownCommands.Value.Contains(id)
        // One command per mounted drive, so the ids only exist at runtime.
        || id.StartsWith(CommandDef.DriveIdPrefix, StringComparison.Ordinal);

    // ── key strokes ──────────────────────────────────────────────────────

    /// <summary>
    /// Parses "ctrl+shift+p" style text. Modifiers come first in any order,
    /// the last part is the key itself.
    /// </summary>
    public static bool TryParseStroke(string text, out KeyStroke stroke)
    {
        stroke = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        string[] parts = text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        // "ctrl++" means Ctrl plus the plus key, which Split just ate.
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

    /// <summary>The names in here are the ones the app itself prints in hints.</summary>
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
