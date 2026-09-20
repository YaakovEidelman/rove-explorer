using Avalonia;
using Avalonia.Headless;
using Rove.UI;

[assembly: AvaloniaTestApplication(typeof(Rove.UI.Tests.TestAppBuilder))]

namespace Rove.UI.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}

public abstract class HeadlessTest
{
    private static readonly HeadlessUnitTestSession _session =
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(HeadlessTest).Assembly);

    protected static Task OnUiThread(Action body) => _session.Dispatch(body, CancellationToken.None);
}
