using System;
using System.Collections.Generic;

namespace Rove.UI.Services;

public sealed record PickerFilter(string Name, string[] Patterns);

public sealed record PickerLaunchOptions(
    string StartDirectory,
    string OutputFile,
    bool Multiple,
    bool Directory,
    PickerFilter[] Filters,
    int SelectedFilterIndex,
    string? ParentWindow)
{
    public static PickerLaunchOptions? Parse(string[] args)
    {
        if (!HasFlag(args, "--picker"))
            return null;

        string? startDirectory = GetOption(args, "--start-dir");
        string? outputFile = GetOption(args, "--out");
        if (startDirectory is null || outputFile is null)
            return null;

        PickerFilter[] filters = GetFilters(args);
        int selected = int.TryParse(GetOption(args, "--filter-selected"), out int i) ? i : 0;

        return new PickerLaunchOptions(
            startDirectory,
            outputFile,
            Multiple: HasFlag(args, "--multiple"),
            Directory: HasFlag(args, "--directory"),
            Filters: filters,
            SelectedFilterIndex: filters.Length > 0 ? Math.Clamp(selected, 0, filters.Length - 1) : 0,
            ParentWindow: GetOption(args, "--parent-window"));
    }

    private static bool HasFlag(string[] args, string flag) =>
        Array.Exists(args, arg => string.Equals(arg, flag, StringComparison.OrdinalIgnoreCase));

    private static string? GetOption(string[] args, string flag)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }
        return null;
    }

    private static PickerFilter[] GetFilters(string[] args)
    {
        List<PickerFilter> filters = [];
        string? name = null;
        List<string> patterns = [];

        void Flush()
        {
            if (name is not null)
                filters.Add(new PickerFilter(name, [.. patterns]));
        }

        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--filter-name", StringComparison.OrdinalIgnoreCase))
            {
                Flush();
                name = args[i + 1];
                patterns = [];
            }
            else if (string.Equals(args[i], "--filter-pattern", StringComparison.OrdinalIgnoreCase))
            {
                patterns.Add(args[i + 1]);
            }
        }
        Flush();
        return [.. filters];
    }
}
