namespace Rove.Core.Services;

public static class CrashLog
{
    public const int RetentionDays = 30;

    private static readonly object _writeLock = new();

    public static void Write(string directory, string source, Exception exception, DateTimeOffset now)
    {
        try
        {
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, $"{now:yyyy-MM-dd}.log");
            string entry =
                $"[{now:O}] {source}{Environment.NewLine}{exception}{Environment.NewLine}{new string('-', 40)}{Environment.NewLine}";
            lock (_writeLock)
            {
                File.AppendAllText(path, entry);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
        }
    }

    public static void Prune(string directory, DateTimeOffset now)
    {
        try
        {
            if (!Directory.Exists(directory))
                return;
            DateOnly cutoff = DateOnly.FromDateTime(now.Date.AddDays(-RetentionDays));
            foreach (string file in Directory.EnumerateFiles(directory, "*.log"))
            {
                if (DateOnly.TryParseExact(Path.GetFileNameWithoutExtension(file), "yyyy-MM-dd", out DateOnly stamp)
                    && stamp < cutoff)
                {
                    File.Delete(file);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
        }
    }
}
