using Avalonia;
using Avalonia.X11;
using Rove.UI.Services;
using System;

namespace Rove.UI;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things
    // aren't initialized yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        CrashLogging.InstallProcessHooks();

        // Installing and uninstalling are the two things Rove does without
        // opening a window; Installed Apps calls the second one by name.
        if (HasFlag(args, "--dev-uninstall"))
        {
            DesktopInstall.DevUninstall(Console.WriteLine);
            return;
        }
        if (HasFlag(args, "--uninstall"))
        {
            DesktopInstall.Uninstall(Console.WriteLine);
            return;
        }
        if (HasFlag(args, "--install"))
        {
            Console.WriteLine(DesktopInstall.InstallNow());
            return;
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    private static bool HasFlag(string[] args, string flag) =>
        Array.Exists(args, arg => string.Equals(arg, flag, StringComparison.OrdinalIgnoreCase));

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .With(new X11PlatformOptions { WmClass = "rove" })
            .UseWaylandWithFallback()
            .WithInterFont()
            .LogToTrace();
}
