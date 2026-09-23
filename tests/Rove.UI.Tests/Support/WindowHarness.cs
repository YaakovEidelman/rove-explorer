using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Rove.Core;
using Rove.Core.Services;
using Rove.UI.Services;
using Rove.UI.ViewModels;
using Rove.UI.Views;

namespace Rove.UI.Tests;

internal sealed class WindowHarness : IDisposable
{
    public string Root { get; }

    public RoveCore Core { get; }

    public MainWindow Window { get; }

    public MainWindowViewModel Model { get; }

    public BookmarkStore Bookmarks { get; }

    public ContentViewModel Content => Model.ContentPage;

    public TabsViewModel Tabs => Model.Tabs;

    private readonly string _bookmarkFile;

    private readonly string _settingsFile;

    private WindowHarness(
        string root, RoveCore core, MainWindow window, MainWindowViewModel model,
        BookmarkStore bookmarks, string bookmarkFile, string settingsFile)
    {
        Root = root;
        Core = core;
        Window = window;
        Model = model;
        Bookmarks = bookmarks;
        _bookmarkFile = bookmarkFile;
        _settingsFile = settingsFile;
    }

    public static WindowHarness Open(Action<string> fill, IAdminSession? admin = null)
    {
        string root = Path.Combine(Path.GetTempPath(), "rove-ui-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        fill(root);

        CommandRegistry registry = new(KeymapLoad.Empty);
        RoveCore core = new(admin);
        FileClipboard fileClipboard = new();
        FileOperationViewModel operation = new(registry);
        string bookmarkFile = Path.Combine(Path.GetTempPath(), "rove-marks-" + Guid.NewGuid().ToString("N") + ".json");
        BookmarkStore bookmarks = new(bookmarkFile);
        string settingsFile = Path.Combine(Path.GetTempPath(), "rove-settings-" + Guid.NewGuid().ToString("N") + ".json");
        SettingsStore settings = new(settingsFile);
        TabsViewModel tabs = new(
            registry, core, fileClipboard, new RecordingClipboard(), new NullIconCache(), operation, bookmarks, settings);

        tabs.Open(root);

        PaletteViewModel palette = new(registry, settings);
        GlobalSearchViewModel search =
            new(registry, core, new NullIconCache(), () => tabs.Active.SearchRoot);
        ConfirmViewModel confirm = new(registry);
        PreviewViewModel preview = new(registry, core, new NullPreviewLoader());

        BookmarksViewModel bookmarkList = new(registry, bookmarks);
        SettingsViewModel settingsPage = new(registry, settings);
        MainWindowViewModel model = new(
            registry, tabs, palette, search, bookmarkList, confirm, preview, fileClipboard, operation, settingsPage);

        MainWindow window = new() { DataContext = model };
        window.Show();

        WindowHarness harness = new(root, core, window, model, bookmarks, bookmarkFile, settingsFile);
        harness.GoTo(root);
        return harness;
    }

    public void GoTo(string directory)
    {
        _ = Content.SetCurrentDirectoryAsync(directory);
        Settle();
    }

    public void Press(Key key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        Window.KeyPressQwerty(ToPhysical(key), modifiers);
        Settle();
    }

    public void Highlight(string name) =>
        Content.DirectoryListing.ListSelection.SelectPath(Path.Combine(Root, name));

    public string[] Names() => [.. Content.DirectoryListing.Items.Select(i => i.Name)];

    public void Settle()
    {
        const int quietPassesWanted = 3;
        DateTime deadline = DateTime.UtcNow.AddSeconds(10);
        int quiet = 0;
        while (quiet < quietPassesWanted && DateTime.UtcNow < deadline)
        {
            Dispatcher.UIThread.RunJobs();
            quiet = Content.IsLoading || Model.Preview.IsLoading || Model.FileOperation.IsRunning
                || Content.Completions.IsReading
                ? 0
                : quiet + 1;
            Thread.Sleep(1);
        }
    }

    private static PhysicalKey ToPhysical(Key key) => key switch
    {
        Key.Enter => PhysicalKey.Enter,
        Key.Tab => PhysicalKey.Tab,
        Key.Escape => PhysicalKey.Escape,
        Key.Space => PhysicalKey.Space,
        Key.Back => PhysicalKey.Backspace,
        Key.OemQuestion => PhysicalKey.Slash,
        Key.OemPeriod => PhysicalKey.Period,
        Key.OemComma => PhysicalKey.Comma,
        Key.Down => PhysicalKey.ArrowDown,
        Key.Up => PhysicalKey.ArrowUp,
        Key.Left => PhysicalKey.ArrowLeft,
        Key.Right => PhysicalKey.ArrowRight,
        >= Key.A and <= Key.Z => PhysicalKey.A + (key - Key.A),
        >= Key.D0 and <= Key.D9 => PhysicalKey.Digit0 + (key - Key.D0),
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, "No physical key for this test."),
    };

    public void Dispose()
    {
        Window.Close();
        Core.Dispose();
        try
        {
            Directory.Delete(Root, recursive: true);
            File.Delete(_bookmarkFile);
            File.Delete(_settingsFile);
        }
        catch (IOException)
        {
        }
    }
}
