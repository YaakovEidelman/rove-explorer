using Avalonia;
using Avalonia.Headless;
using Rove.UI;

[assembly: AvaloniaTestApplication(typeof(Rove.UI.Tests.TestAppBuilder))]

namespace Rove.UI.Tests;

/// <summary>
/// The real <see cref="App"/> on a windowless backend, so key routing runs
/// through the same styles and the same window the user gets. App only builds
/// its view models under a desktop lifetime, which this session is not — so
/// nothing here touches the user's own files.
/// </summary>
public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}

/// <summary>
/// Base for tests that drive a real window. <see cref="OnUiThread"/> runs the
/// body where Avalonia expects to be — one UI thread for the whole assembly,
/// the same way the app has exactly one.
/// </summary>
public abstract class HeadlessTest
{
    private static readonly HeadlessUnitTestSession _session =
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(HeadlessTest).Assembly);

    protected static Task OnUiThread(Action body) => _session.Dispatch(body, CancellationToken.None);
}
