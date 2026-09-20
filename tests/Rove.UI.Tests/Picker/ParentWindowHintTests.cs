using Avalonia.Controls;
using Rove.UI.Services;
using Xunit;

namespace Rove.UI.Tests;

public class ParentWindowHintTests : HeadlessTest
{
    [Fact]
    public Task NullParentWindowIsANoOp() => OnUiThread(() =>
    {
        Window window = new();
        ParentWindowHint.Apply(window, null);
        window.Show();
        window.Close();
    });

    [Fact]
    public Task UnsupportedWaylandPrefixIsANoOp() => OnUiThread(() =>
    {
        Window window = new();
        ParentWindowHint.Apply(window, "wayland:some-handle");
        window.Show();
        window.Close();
    });

    [Fact]
    public Task MalformedX11HandleIsIgnored() => OnUiThread(() =>
    {
        Window window = new();
        ParentWindowHint.Apply(window, "x11:not-hex");
        window.Show();
        window.Close();
    });

    [Fact]
    public Task WellFormedX11HandleDoesNotThrowOnAHeadlessWindow() => OnUiThread(() =>
    {
        Window window = new();
        ParentWindowHint.Apply(window, "x11:1a2b3c");
        window.Show();
        window.Close();
    });
}
