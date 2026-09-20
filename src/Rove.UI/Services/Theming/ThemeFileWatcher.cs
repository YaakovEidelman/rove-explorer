using System.Runtime.Versioning;
using Avalonia.Threading;
using Rove.Core.Services;

namespace Rove.UI.Services;

public sealed class ThemeFileWatcher : IDisposable
{
    private static readonly TimeSpan DebounceWindow = TimeSpan.FromMilliseconds(250);

    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly Lock _gate = new();
    private readonly Action _onChanged;
    private Timer? _debounce;

    public ThemeFileWatcher(Action onChanged)
    {
        _onChanged = onChanged;

        string themeDir = Path.GetDirectoryName(RovePaths.CustomThemeFile)!;
        AddWatcher(themeDir, Path.GetFileName(RovePaths.CustomThemeFile));

        if (OperatingSystem.IsLinux())
            AddOmarchyWatcher();
    }

    [SupportedOSPlatform("linux")]
    private void AddOmarchyWatcher()
    {
        string dir = OmarchyTheme.CurrentDirectory;
        AddWatcher(dir, "*");
    }

    private void AddWatcher(string directory, string filter)
    {
        if (!Directory.Exists(directory))
            return;

        FileSystemWatcher watcher = new(directory, filter)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
        };
        watcher.Changed += (_, _) => Schedule();
        watcher.Created += (_, _) => Schedule();
        watcher.Deleted += (_, _) => Schedule();
        watcher.Renamed += (_, _) => Schedule();
        watcher.Error += (_, _) => Schedule();
        watcher.EnableRaisingEvents = true;
        _watchers.Add(watcher);
    }

    private void Schedule()
    {
        lock (_gate)
        {
            _debounce?.Dispose();
            _debounce = new Timer(_ => Dispatcher.UIThread.Post(_onChanged), null, DebounceWindow, Timeout.InfiniteTimeSpan);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _debounce?.Dispose();
            _debounce = null;
        }
        foreach (FileSystemWatcher watcher in _watchers)
            watcher.Dispose();
    }
}
