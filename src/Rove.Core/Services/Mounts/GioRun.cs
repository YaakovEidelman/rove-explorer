namespace Rove.Core.Services;

internal readonly record struct GioRun(int ExitCode, string Output, string ErrorText, bool Prompted)
{
    public string Error(string fallback)
    {
        string? last = ErrorText.Split('\n')
            .Select(line => line.Trim())
            .LastOrDefault(line => line.Length > 0);
        if (last is null)
            return fallback;
        if (last.StartsWith("gio: ", StringComparison.Ordinal))
            last = last[5..];
        int split = last.IndexOf(": ", StringComparison.Ordinal);
        if (split > 0 && (last[..split].Contains("://", StringComparison.Ordinal) || last[0] == '/'))
            last = last[(split + 2)..];
        return last.Length > 0 ? last : fallback;
    }
}
