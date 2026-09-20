using System.ComponentModel;
using System.Diagnostics;

namespace Rove.Core.Services;

public static class GioMime
{
    private static readonly TimeSpan _patience = TimeSpan.FromSeconds(3);

    public static string? ContentType(string path) =>
        ParseContentType(Run("info", "-a", "standard::content-type", LongPath.Display(path)));

    public static string[] AppIds(string contentType) => ParseAppIds(Run("mime", contentType));

    internal static string? ParseContentType(string output)
    {
        foreach (string line in output.Split('\n'))
        {
            const string key = "standard::content-type:";
            int at = line.IndexOf(key, StringComparison.Ordinal);
            if (at >= 0 && line[(at + key.Length)..].Trim() is { Length: > 0 } type)
                return type;
        }
        return null;
    }

    internal static string[] ParseAppIds(string output)
    {
        List<string> defaults = [];
        List<string> recommended = [];
        List<string> registered = [];
        List<string>? section = null;

        foreach (string raw in output.Split('\n'))
        {
            string line = raw.TrimEnd();
            if (line.StartsWith("Default application for", StringComparison.Ordinal))
            {
                int colon = line.LastIndexOf(": ", StringComparison.Ordinal);
                if (colon >= 0)
                    defaults.Add(line[(colon + 2)..].Trim());
                section = null;
            }
            else if (line.StartsWith("Registered applications", StringComparison.Ordinal))
            {
                section = registered;
            }
            else if (line.StartsWith("Recommended applications", StringComparison.Ordinal))
            {
                section = recommended;
            }
            else if (line.StartsWith('\t') && section is not null && line.EndsWith(".desktop", StringComparison.Ordinal))
            {
                section.Add(line.Trim());
            }
            else if (line.Length > 0 && !line.StartsWith('\t'))
            {
                section = null;
            }
        }

        return [.. defaults.Concat(recommended).Concat(registered).Distinct(StringComparer.Ordinal)];
    }

    private static string Run(params string[] arguments)
    {
        string? gio = FileOpener.FindProgram(
            Environment.GetEnvironmentVariable("PATH"), "gio", FileOpener.IsRunnable);
        if (gio is null)
            return string.Empty;

        ProcessStartInfo info = new(gio)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
        };
        info.Environment["LC_ALL"] = "C";
        info.Environment["LANGUAGE"] = "C";
        foreach (string argument in arguments)
            info.ArgumentList.Add(argument);

        try
        {
            using Process? process = Process.Start(info);
            if (process is null)
                return string.Empty;

            process.StandardInput.Close();
            Task<string> output = process.StandardOutput.ReadToEndAsync();
            _ = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(_patience))
            {
                process.Kill();
                return string.Empty;
            }
            return output.GetAwaiter().GetResult();
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException)
        {
            return string.Empty;
        }
    }
}
