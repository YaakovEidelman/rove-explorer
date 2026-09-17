using Avalonia.Controls;
using System;
using System.Globalization;
using System.Runtime.InteropServices;

namespace Rove.UI.Services;

/// <summary>
/// Correlates the picker window with the caller's window, the way the
/// FileChooser portal spec expects (org.freedesktop.impl.portal.FileChooser's
/// parent_window argument), so window managers show the picker transient-for
/// whatever asked for it instead of as an unrelated top-level.
/// </summary>
public static class ParentWindowHint
{
    private const string X11Prefix = "x11:";

    public static void Apply(Window window, string? parentWindow)
    {
        if (string.IsNullOrEmpty(parentWindow))
            return;

        // Wayland's zxdg_importer_v1 isn't bound by Avalonia.Wayland yet (only
        // the exporter side, zxdg_exporter_v2, is — see
        // src/Rove.UI/TODO.md) so a "wayland:HANDLE" parent can't be honored
        // here today.
        if (!parentWindow.StartsWith(X11Prefix, StringComparison.Ordinal))
            return;

        string hex = parentWindow[X11Prefix.Length..];
        if (!ulong.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong parentXid))
            return;

        window.Opened += (_, _) => SetTransientForX11(window, (IntPtr)parentXid);
    }

    private static void SetTransientForX11(Window window, IntPtr parentXid)
    {
        if (window.TryGetPlatformHandle() is not { HandleDescriptor: "XID" } handle)
            return;

        IntPtr display = XOpenDisplay(IntPtr.Zero);
        if (display == IntPtr.Zero)
            return;
        try
        {
            XSetTransientForHint(display, handle.Handle, parentXid);
            XFlush(display);
        }
        finally
        {
            XCloseDisplay(display);
        }
    }

    [DllImport("libX11.so.6")]
    private static extern IntPtr XOpenDisplay(IntPtr displayName);

    [DllImport("libX11.so.6")]
    private static extern int XSetTransientForHint(IntPtr display, IntPtr window, IntPtr parent);

    [DllImport("libX11.so.6")]
    private static extern int XFlush(IntPtr display);

    [DllImport("libX11.so.6")]
    private static extern int XCloseDisplay(IntPtr display);
}
