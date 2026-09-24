using Avalonia;
using Avalonia.X11;
using Rove.UI.Services;
using System;

namespace Rove.UI;

sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        CrashLogging.InstallProcessHooks();

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
