using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Rove.Core;
using Rove.Core.Services;
using Rove.UI.Services;
using Rove.UI.ViewModels;
using Rove.UI.Views;
using System;
using System.Net.Http;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace Rove.UI;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            CrashLogging.InstallDispatcherHook();
            CrashLogging.PruneInBackground();

            if (PickerLaunchOptions.Parse(desktop.Args ?? []) is { } picker)
            {
                StartPicker(desktop, picker);
                base.OnFrameworkInitializationCompleted();
                return;
            }

            // Off on its own thread: nothing here waits for an install, and a
            // launch with nothing to install reads one small file and stops.
            DesktopInstall.EnsureInBackground();
            DefaultConfigFiles.EnsureExist();

            Func<IClipboard?> clipboardFactory = () => TopLevel.GetTopLevel(desktop.MainWindow)?.Clipboard;
            Func<IStorageProvider?> storageProviderFactory = () => TopLevel.GetTopLevel(desktop.MainWindow)?.StorageProvider;
            RoveClipboardService systemClipboard = new(clipboardFactory, storageProviderFactory);

            // The user's keybindings file is optional; without one the
            // built-in defaults are the whole keymap.
            KeymapLoad keymap = KeymapConfig.LoadDefault();
            CommandRegistry registry = new(keymap);
            RoveCore core = new();
            FileClipboard fileClipboard = new();
            IconCache iconCache = new(core.Actions.GetItemIcon);
            FileOperationViewModel fileOperation = new(registry);
            BookmarkStore bookmarks = new();
            SettingsStore settings = new();

            TabsViewModel tabs = new(
                registry, core, fileClipboard, systemClipboard, iconCache, fileOperation, bookmarks, settings);

            string? explicitTarget = StartLocation.ExplicitTarget(desktop.Args, Directory.Exists, File.Exists);
            tabs.Open(explicitTarget ?? PathCompare.DefaultStartDirectory());

            PaletteViewModel palette = new(registry);
            GlobalSearchViewModel globalSearch = new(registry, core, iconCache, () => tabs.Active.SearchRoot);
            palette.Opening += () => tabs.Active.RefreshDriveCommands();
            BookmarksViewModel bookmarkList = new(registry, bookmarks);
            ConfirmViewModel confirm = new(registry);
            PreviewViewModel preview = new(registry, core, new ImagePreviewLoader());
            SettingsViewModel settingsPage = new(registry, settings, confirm: confirm);
            ThemeFileWatcher themeWatcher = new(settingsPage.ApplyTheme);

            HttpClient updateHttp = new() { Timeout = TimeSpan.FromSeconds(10) };
            UpdateService updates = new(updateHttp);
            // Off on its own thread, same as the self-install check above: a
            // check that never gets to run — no network, GitHub unreachable
            // — is not a reason to make the window wait.
            _ = Task.Run(() => updates.CheckInBackgroundAsync(settings.Current.AutoUpdate, default));

            MainWindowViewModel main = new(registry, tabs, palette, globalSearch, bookmarkList, confirm,
                preview, fileClipboard, fileOperation, settingsPage, keymap.Summary, updates);
            desktop.MainWindow = new MainWindow
            {
                DataContext = main,
            };

            if (OperatingSystem.IsLinux())
                OfferDefaultFilePicker(confirm);

            desktop.ShutdownRequested += (_, _) =>
            {
                themeWatcher.Dispose();
                tabs.Dispose();
                core.Dispose();
                updateHttp.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    [SupportedOSPlatform("linux")]
    private static void OfferDefaultFilePicker(ConfirmViewModel confirm)
    {
        FilePickerPortal portal = new();
        if (portal.Status != PortalStatus.NotInstalled || portal.HasAskedAboutDefault)
            return;

        portal.MarkAskedAboutDefault();
        confirm.Request(FilePickerPortal.ClaimWarning, () => portal.Enable());
    }

    private static void StartPicker(IClassicDesktopStyleApplicationLifetime desktop, PickerLaunchOptions picker)
    {
        Func<IClipboard?> clipboardFactory = () => TopLevel.GetTopLevel(desktop.MainWindow)?.Clipboard;
        Func<IStorageProvider?> storageProviderFactory = () => TopLevel.GetTopLevel(desktop.MainWindow)?.StorageProvider;
        RoveClipboardService systemClipboard = new(clipboardFactory, storageProviderFactory);

        KeymapLoad keymap = KeymapConfig.LoadDefault();
        CommandRegistry registry = new(keymap);
        RoveCore core = new();
        FileClipboard fileClipboard = new();
        IconCache iconCache = new(core.Actions.GetItemIcon);
        FileOperationViewModel fileOperation = new(registry);
        BookmarkStore bookmarks = new();
        SettingsStore settings = new();
        UndoStack undo = new();

        ThemePalette.ApplyFromSettings(settings.Current);

        ContentViewModel content = new(
            registry, core, fileClipboard, systemClipboard, iconCache, fileOperation, bookmarks, undo, settings);
        string startDirectory = Directory.Exists(picker.StartDirectory)
            ? picker.StartDirectory
            : PathCompare.DefaultStartDirectory();
        _ = content.SetCurrentDirectoryAsync(startDirectory);
        if (picker.Filters.Length > 0)
            content.DirectoryListing.SetSelectionFilter(picker.Filters[picker.SelectedFilterIndex].Patterns);

        ConfirmViewModel confirm = new(registry);

        PickerWindowViewModel main = new(
            registry, content, confirm, fileClipboard, fileOperation, picker.Directory, picker.Multiple,
            picker.OutputFile, picker.Filters, picker.SelectedFilterIndex);

        PickerWindow window = new()
        {
            DataContext = main,
        };
        ParentWindowHint.Apply(window, picker.ParentWindow);
        desktop.MainWindow = window;
        desktop.ShutdownRequested += (_, _) => core.Dispose();
    }
}
