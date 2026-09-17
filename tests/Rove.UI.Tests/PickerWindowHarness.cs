using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Rove.Core;
using Rove.UI.Services;
using Rove.UI.ViewModels;
using Rove.UI.Views;

namespace Rove.UI.Tests;

/// <summary>
/// A real <see cref="PickerWindow"/> on the headless backend, showing a real
/// folder. Confirming or cancelling would normally call
/// <c>Environment.Exit</c> straight out from under the test process — both
/// the window's own <c>Closed</c> handler and the view model's exit calls
/// are disarmed here, with the exit code recorded instead.
/// </summary>
internal sealed class PickerWindowHarness : IDisposable
{
    public string Root { get; }

    public RoveCore Core { get; }

    public PickerWindow Window { get; }

    public PickerWindowViewModel Model { get; }

    public ContentViewModel Content => Model.ContentPage;

    public string OutputFile { get; }

    public List<int> ExitCodes { get; }

    private readonly string _bookmarkFile;
    private readonly string _settingsFile;

    private PickerWindowHarness(
        string root, RoveCore core, PickerWindow window, PickerWindowViewModel model,
        string outputFile, List<int> exitCodes, string bookmarkFile, string settingsFile)
    {
        Root = root;
        Core = core;
        Window = window;
        Model = model;
        OutputFile = outputFile;
        ExitCodes = exitCodes;
        _bookmarkFile = bookmarkFile;
        _settingsFile = settingsFile;
    }

    public static PickerWindowHarness Open(
        Action<string> fill, bool multiple = false, bool directory = false,
        PickerFilter[]? filters = null, int filterIndex = 0)
    {
        string root = Path.Combine(Path.GetTempPath(), "rove-picker-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        fill(root);

        CommandRegistry registry = new(KeymapLoad.Empty);
        RoveCore core = new();
        FileClipboard fileClipboard = new();
        FileOperationViewModel operation = new(registry);
        string bookmarkFile =
            Path.Combine(Path.GetTempPath(), "rove-picker-marks-" + Guid.NewGuid().ToString("N") + ".json");
        BookmarkStore bookmarks = new(bookmarkFile);
        string settingsFile =
            Path.Combine(Path.GetTempPath(), "rove-picker-settings-" + Guid.NewGuid().ToString("N") + ".json");
        SettingsStore settings = new(settingsFile);
        UndoStack undo = new();

        ContentViewModel content = new(
            registry, core, fileClipboard, new RecordingClipboard(), new NullIconCache(), operation,
            bookmarks, undo, settings);
        // The constructor queues a load of whatever CurrentDir is once the
        // dispatcher gets to it; setting it to root now (before that runs)
        // means that queued load lands on root too, instead of racing a
        // separate navigation to it — see TabsViewModel.Open for the same fix.
        content.DirectoryListing.CurrentDir = root;
        if (filters is { Length: > 0 })
            content.DirectoryListing.SetSelectionFilter(filters[filterIndex].Patterns);

        string outputFile =
            Path.Combine(Path.GetTempPath(), "rove-picker-out-" + Guid.NewGuid().ToString("N") + ".txt");
        ConfirmViewModel confirm = new(registry);

        List<int> exitCodes = [];
        PickerWindowViewModel model = new(
            registry, content, confirm, fileClipboard, operation, directory, multiple, outputFile,
            filters, filterIndex, exitCodes.Add);

        PickerWindow window = new() { DataContext = model, ExitOnClose = false };
        window.Show();

        PickerWindowHarness harness =
            new(root, core, window, model, outputFile, exitCodes, bookmarkFile, settingsFile);
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

    public string[] OutputLines() => File.Exists(OutputFile) ? File.ReadAllLines(OutputFile) : [];

    public void Settle()
    {
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
        Window.ExitOnClose = false;
        Window.Close();
        Core.Dispose();
        try
        {
            Directory.Delete(Root, recursive: true);
            File.Delete(_bookmarkFile);
            File.Delete(_settingsFile);
            File.Delete(OutputFile);
        }
        catch (IOException)
        {
        }
    }
}
