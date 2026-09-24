using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;

namespace Rove.UI.ViewModels;

public partial class MainWindowViewModel
{
    private void CloseApp()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
    }

    private static void ToggleTheme()
    {
        if (Application.Current is not { } app)
            return;
        app.RequestedThemeVariant =
            app.ActualThemeVariant == ThemeVariant.Dark ? ThemeVariant.Light : ThemeVariant.Dark;
    }

    private void CheckForUpdates()
    {
        if (_updates is null)
        {
            StatusError = "Updates aren't available in this build.";
            return;
        }
        _ = _updates.CheckNowAsync(CancellationToken.None);
    }

    private static void ToggleMaximize()
    {
        if (MainWindowInstance() is not { } window)
            return;
        window.WindowState =
            window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private static void MinimizeWindow()
    {
        if (MainWindowInstance() is { } window)
            window.WindowState = WindowState.Minimized;
    }

    private static Window? MainWindowInstance() =>
        Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } window }
            ? window
            : null;
}
