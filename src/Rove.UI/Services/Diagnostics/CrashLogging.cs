using Avalonia.Threading;
using Rove.Core.Services;

namespace Rove.UI.Services;

public static class CrashLogging
{
    public static void InstallProcessHooks()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Log("AppDomain", e.ExceptionObject as Exception ?? new Exception(e.ExceptionObject?.ToString()));

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log("UnobservedTask", e.Exception);
            e.SetObserved();
        };
    }

    public static void InstallDispatcherHook()
    {
        Dispatcher.UIThread.UnhandledException += (_, e) => Log("Dispatcher", e.Exception);
    }

    public static void PruneInBackground()
    {
        _ = Task.Run(() => CrashLog.Prune(RovePaths.LogDirectory, DateTimeOffset.Now));
    }

    private static void Log(string source, Exception exception) =>
        CrashLog.Write(RovePaths.LogDirectory, source, exception, DateTimeOffset.Now);
}
