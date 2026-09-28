using System.Text.RegularExpressions;

namespace Rove.Core.Services;

public sealed partial record GioPrompt(string Field, string? Suggested, string Message, string[] Choices)
{
    [GeneratedRegex(@"(?:^|\n)(?<field>User|Domain|Password|Choice)(?: \[(?<suggested>[^\]\n]*)\])?: $")]
    private static partial Regex Waiting();

    [GeneratedRegex(@"^\[(?<number>\d+)\] (?<text>.*)$")]
    private static partial Regex ChoiceLine();

    public bool IsSecret => Field == "Password";

    public bool IsChoice => Field == "Choice";

    public static GioPrompt? TryRead(string output)
    {
        if (Waiting().Match(output) is not { Success: true } waiting)
            return null;

        List<string> message = [];
        List<string> choices = [];
        foreach (string raw in output[..waiting.Index].Split('\n'))
        {
            string line = raw.TrimEnd('\r').Trim();
            if (line.Length == 0)
                continue;
            if (ChoiceLine().Match(line) is { Success: true } choice)
                choices.Add(choice.Groups["text"].Value);
            else
                message.Add(line);
        }

        string? suggested = waiting.Groups["suggested"] is { Success: true, Length: > 0 } group ? group.Value : null;
        return new GioPrompt(waiting.Groups["field"].Value, suggested, string.Join("\n", message), [.. choices]);
    }
}
