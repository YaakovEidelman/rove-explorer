# Rove icons

Background: none, transparent canvas. Palette: panel `#CBD3DB`, needle north
`#2D5DA8`, needle outline `#4E6478`. Grid: 64 units, artwork scaled 1.15x on
the grid (no plate), needle pivot centred on the panel body (tab excluded),
39 degree bearing.

## Files

| File | Use |
| --- | --- |
| `rove.svg` | The master. Wherever SVG is accepted. |
| `rove-16.svg`, `rove-24.svg`, `rove-32.svg` | Redrawn for small sizes: thicker needle outline and a slightly larger panel, because the master's 1.8-unit outline vanishes below ~40px. |
| `rove-symbolic.svg` | 16x16 grid, single colour via `currentColor`, panel at 35% opacity. Titlebars, trays, symbolic contexts. |
| `png/rove-*.png` | Rasterised from the size-matched SVG above. |
| `rove.ico` | Windows icon: BMP frames at 16-128, PNG at 256. |

`rove.ico`, `rove.svg`, `rove-symbolic.svg` and every `png/rove-*.png` are
compiled into the binary as plain embedded resources, named `rove.ico`,
`rove.svg`, `rove-symbolic.svg` and `png/rove-16.png` and so on. The window
icon is read from there, and so are the icons the installer writes into the
hicolor theme on Linux — deliberately not Avalonia resources, because
`--install` runs with no UI and no asset loader to ask. The size-specific SVGs
are sources for the PNGs, not shipped.

## Regenerating

`rove.ico` is built from the PNGs. Sub-256 frames stay uncompressed BMP
because some Windows shell paths still do not decode PNG frames below 256.

```sh
python - <<'PY'
import struct
from PIL import Image

def dib(img):
    w, h = img.size
    px = img.load()
    xor = b"".join(
        bytes(v for x in range(w) for v in (lambda r, g, b, a: (b, g, r, a))(*px[x, y]))
        for y in range(h - 1, -1, -1))
    and_mask = b"\x00" * ((w + 31) // 32 * 4 * h)
    return struct.pack("<IiiHHIIiiII", 40, w, h * 2, 1, 32, 0,
                       len(xor) + len(and_mask), 0, 0, 0, 0) + xor + and_mask

entries = [(n, dib(Image.open(f"png/rove-{n}.png").convert("RGBA")))
           for n in (16, 24, 32, 48, 64, 128)]
entries.append((256, open("png/rove-256.png", "rb").read()))

head = struct.pack("<HHH", 0, 1, len(entries))
offset = 6 + 16 * len(entries)
for n, data in entries:
    head += struct.pack("<BBBBHHII", n % 256, n % 256, 0, 0, 1, 32, len(data), offset)
    offset += len(data)
open("rove.ico", "wb").write(head + b"".join(d for _, d in entries))
PY
```

## Installing on Linux

Rove installs itself — see `LinuxInstall` and `docs/installing.md`. The entry
points at `Icon=rove`, and the window's `WM_CLASS` is pinned to `rove` in
`Program.cs` so docks match the two up. A packaged install (an entry in
`/usr/share/applications`) is left alone.
