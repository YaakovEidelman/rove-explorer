using System.Diagnostics;

namespace Rove.Portal;

internal sealed class RovePickerSession : IDisposable
{
    private const int CancelExitCode = 1;

    private readonly Process _process;
    private readonly string _outputFile;

    private RovePickerSession(Process process, string outputFile)
    {
        _process = process;
        _outputFile = outputFile;
    }

    public static RovePickerSession Start(
        string startDirectory, bool multiple, bool directory,
        (string Name, string[] Patterns)[] filters, int selectedFilterIndex, string parentWindow)
    {
        string outputFile = Path.Combine(Path.GetTempPath(), $"rove-portal-{Guid.NewGuid():N}.txt");

        ProcessStartInfo info = new(ExecutablePath()) { UseShellExecute = false };
        info.ArgumentList.Add("--picker");
        info.ArgumentList.Add("--start-dir");
        info.ArgumentList.Add(startDirectory);
        info.ArgumentList.Add("--out");
        info.ArgumentList.Add(outputFile);
        if (parentWindow.Length > 0)
        {
            info.ArgumentList.Add("--parent-window");
            info.ArgumentList.Add(parentWindow);
        }
        if (multiple)
            info.ArgumentList.Add("--multiple");
        if (directory)
            info.ArgumentList.Add("--directory");
        foreach ((string name, string[] patterns) in filters)
        {
            info.ArgumentList.Add("--filter-name");
            info.ArgumentList.Add(name);
            foreach (string pattern in patterns)
            {
                info.ArgumentList.Add("--filter-pattern");
                info.ArgumentList.Add(pattern);
            }
        }
        if (filters.Length > 0)
        {
            info.ArgumentList.Add("--filter-selected");
            info.ArgumentList.Add(selectedFilterIndex.ToString());
        }

        Process process = Process.Start(info) ?? throw new InvalidOperationException("rove did not start.");
        return new RovePickerSession(process, outputFile);
    }

    public async Task<string[]?> WaitForResultAsync()
    {
        await _process.WaitForExitAsync();

        if (_process.ExitCode == CancelExitCode || !File.Exists(_outputFile))
            return null;

        string[] lines = await File.ReadAllLinesAsync(_outputFile);
        return [.. lines.Where(l => l.Length > 0)];
    }

    public void Cancel()
    {
        try
        {
            if (!_process.HasExited)
                _process.Kill();
        }
        catch (InvalidOperationException)
        {
        }
    }

    public void Dispose()
    {
        try
        {
            File.Delete(_outputFile);
        }
        catch (IOException)
        {
        }
        _process.Dispose();
    }

    private static string ExecutablePath()
    {
        string installed = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin", "rove");
        return File.Exists(installed) ? installed : "rove";
    }
}
