# TODO — Linux file picker portal

Linux only, none of this touches Windows. Each numbered item is one commit.
Delete an item once it's built and proven working. When the list is empty,
delete this whole file.

1. ~~Correlate the caller's parent_window so the picker shows up~~ Done for
   X11: `parent_window` now flows from `FileChooserHandler` through
   `RovePickerSession` (`--parent-window`) to `ParentWindowHint.Apply`, which
   calls `XSetTransientForHint` once the picker window opens. Verified with
   `xprop`: a picker launched with `--parent-window x11:<hex>` reports
   `WM_TRANSIENT_FOR` pointing at that XID.

   Still blocked on Wayland, **not blocking release**: `parent_window`
   there is a `wayland:HANDLE` string obtained via the caller's
   `zxdg_exporter_v1`/`v2`, and honoring it means the picker importing that
   handle (`zxdg_importer_v1`) and calling `set_parent_of` on its own
   surface. Avalonia.Wayland 12.1.2 only binds the exporter side
   (`zxdg_exporter_v2`, via the internal `IWaylandXdgTopLevelExport`) —
   there's no importer binding and no public hook to reach the underlying
   `wl_display`/`wl_surface` to add one ourselves. The picker still opens
   and works on Wayland without this — it just won't be pinned above the
   calling app's window.

   No upstream Avalonia issue/PR tracks the importer specifically as of
   2026-09-16; closest is AvaloniaUI/Avalonia#22223 (open), which asks for
   exposing the raw `wl_surface`/`wl_display` handle at all — confirms
   there's no public hook today even for what we already use. Reflecting
   into Avalonia.Wayland's internals from Rove isn't viable: the relevant
   types (`IWaylandXdgTopLevelExport`, `WaylandGlobals`, `WXdgTopLevel`) are
   `internal` with `InternalsVisibleTo` scoped only to `NWayland.Server`,
   and internal members like that get trimmed under our `PublishAot` build
   regardless. The realistic fix is forking/vendoring `Avalonia.Wayland`
   (submodule + local `ProjectReference` instead of the NuGet package) and
   adding a `zxdg_importer_v2` binding ourselves, mirroring the existing
   exporter: bind it in `WaylandGlobals.cs` next to `XdgExporter`, add an
   `XdgToplevelImport.cs` worker class shaped like `XdgToplevelExport.cs`
   (~50-80 lines), and expose one public entry point on `WindowImpl`. Rough
   estimate: 3-4 files, 150-250 LOC — the underlying NWayland-generated
   protocol bindings for the importer almost certainly already exist (the
   vendored wayland-protocols XML that feeds NWayland's generator includes
   all of xdg-foreign, exporter and importer both), Avalonia.Wayland just
   never wires them up. Revisit once Avalonia.Wayland adds importer support
   upstream, or pick up the fork then.
