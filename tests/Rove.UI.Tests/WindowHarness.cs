using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Rove.Core;
using Rove.UI.Services;
using Rove.UI.ViewModels;
using Rove.UI.Views;

namespace Rove.UI.Tests;

/// <summary>Copies nothing anywhere: "copy path" has to land somewhere in tests.</summary>
internal sealed class RecordingClipboard : IRoveClipboardService
{
    public string? LastText { get; private set; }

    public Task CopyTextAsync(string text)
    {
        LastText = text;
        return Task.CompletedTask;
    }

    public Task SetFilesAsync(IReadOnlyList<string> paths, ClipboardOp op) => Task.CompletedTask;

    public Task<(IReadOnlyList<string> Paths, ClipboardOp Op)?> TryGetFilesAsync() =>
        Task.FromResult<(IReadOnlyList<string> Paths, ClipboardOp Op)?>(null);
}

/// <summary>
/// A real <see cref="MainWindow"/> on the headless backend, showing a real
/// folder. Keys go in the way the user's keys go in — through the platform,
/// the window, and whatever happens to hold focus — which is the only way to
/// catch a break in the routing between them.
/// </summary>
internal sealed class WindowHarness : IDisposable
{
    public string Root { get; }

    public RoveCore Core { get; }

    public MainWindow Window { get; }

    public MainWindowViewModel Model { get; }

    public BookmarkStore Bookmarks { get; }

    public ContentViewModel Content => Model.ContentPage;

    public TabsViewModel Tabs => Model.Tabs;

    /// <summary>Where the bookmarks are written — outside the folder on screen.</summary>
    private readonly string _bookmarkFile;

    /// <summary>Where settings are written — outside the folder on screen.</summary>
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

    public static WindowHarness Open(Action<string> fill)
    {
        string root = Path.Combine(Path.GetTempPath(), "rove-ui-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        fill(root);

        CommandRegistry registry = new(KeymapLoad.Empty);
        RoveCore core = new();
        FileClipboard fileClipboard = new();
        FileOperationViewModel operation = new(registry);
        string bookmarkFile = Path.Combine(Path.GetTempPath(), "rove-marks-" + Guid.NewGuid().ToString("N") + ".json");
        BookmarkStore bookmarks = new(bookmarkFile);
        string settingsFile = Path.Combine(Path.GetTempPath(), "rove-settings-" + Guid.NewGuid().ToString("N") + ".json");
        SettingsStore settings = new(settingsFile);
        TabsViewModel tabs = new(
            registry, core, fileClipboard, new RecordingClipboard(), new NullIconCache(), operation, bookmarks, settings);

        // The app's first listing is whatever the tab's folder says when the
        // queued load runs, and a view starts life on the user's home folder.
        // Opening the first tab on the scratch folder is what keeps these
        // tests off the machine they run on.
        tabs.Open(root);

        PaletteViewModel palette = new(registry);
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

    /// <summary>Navigates and settles, so the list is the folder's real contents.</summary>
    public void GoTo(string directory)
    {
        _ = Content.SetCurrentDirectoryAsync(directory);
        Settle();
    }

    /// <summary>Presses a key at the platform, exactly as the user would.</summary>
    public void Press(Key key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        Window.KeyPressQwerty(ToPhysical(key), modifiers);
        Settle();
    }

    public void Highlight(string name) =>
        Content.DirectoryListing.ListSelection.SelectPath(Path.Combine(Root, name));

    public string[] Names() => [.. Content.DirectoryListing.Items.Select(i => i.Name)];

    /// <summary>
    /// Runs everything the UI queued and waits out work still in flight — a
    /// folder being read, a file operation running. Both are asynchronous and
    /// both come back to this very thread, so the wait has to keep the
    /// thread's own queue running rather than block on the task.
    /// </summary>
    public void Settle()
    {
        // Several quiet passes, not one: a key press is delivered by the
        // queue as well, so the first pass can find nothing happening simply
        // because the key has not been handed over yet.
        const int quietPassesWanted = 3;
        DateTime deadline = DateTime.UtcNow.AddSeconds(10);
        int quiet = 0;
        while (quiet < quietPassesWanted && DateTime.UtcNow < deadline)
        {
            Dispatcher.UIThread.RunJobs();
            quiet = Content.IsLoading || Model.FileOperation.IsRunning || Content.Completions.IsReading
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
