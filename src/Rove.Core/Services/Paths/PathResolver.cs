using System.Text;

namespace Rove.Core.Services;

public static class PathResolver
{
    public static string? Resolve(string input, string currentDirectory)
    {
        if (input is null)
            return null;

        string text = ExpandHome(ExpandVariables(Unquote(input)));
        if (text.Length == 0)
            return null;

        try
        {
            string baseDir = Path.GetFullPath(LongPath.Display(currentDirectory));
            string full = Path.IsPathRooted(text)
                ? Path.GetFullPath(text)
                : Path.GetFullPath(text, baseDir);
            return Tidy(full);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    private static string Unquote(string input)
    {
        string text = input.Trim();
        if (text.Length >= 2
            && ((text[0] == '"' && text[^1] == '"') || (text[0] == '\'' && text[^1] == '\'')))
        {
            text = text[1..^1].Trim();
        }
        return text;
    }

    private static string ExpandHome(string text)
    {
        if (text.Length == 0 || text[0] != '~')
            return text;
        if (text.Length > 1 && text[1] != '/' && text[1] != Path.DirectorySeparatorChar)
            return text;

        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (home.Length == 0)
            return text;
        return text.Length == 1 ? home : Path.Combine(home, text[2..].TrimStart('/', Path.DirectorySeparatorChar));
    }

    private static string ExpandVariables(string text)
    {
        if (text.Length == 0)
            return text;
        if (OperatingSystem.IsWindows())
            return Environment.ExpandEnvironmentVariables(text);

        StringBuilder result = new(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] != '$')
            {
                result.Append(text[i]);
                continue;
            }

            bool braced = i + 1 < text.Length && text[i + 1] == '{';
            int start = braced ? i + 2 : i + 1;
            int end = start;
            while (end < text.Length && (char.IsAsciiLetterOrDigit(text[end]) || text[end] == '_'))
                end++;

            if (end == start || (braced && (end >= text.Length || text[end] != '}')))
            {
                result.Append(text[i]);
                continue;
            }

            string name = text[start..end];
            string? value = Environment.GetEnvironmentVariable(name);
            result.Append(value ?? text[i..(braced ? end + 1 : end)]);
            i = (braced ? end + 1 : end) - 1;
        }
        return result.ToString();
    }

    private static string Tidy(string full)
    {
        string trimmed = Path.TrimEndingDirectorySeparator(full);
        return trimmed.Length == 0 ? full : trimmed;
    }
}
