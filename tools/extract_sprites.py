"""Decode a MonoGame/Android XNB Texture2D into a PIL image, so we can crop real
game sprites (Junimo Chest, Stone Junimo) and recolor them for the mod.

Dev tool only; not shipped. Usage is driven by the functions below / __main__.
"""
import colorsys
import os
import struct
import lz4.block
from PIL import Image

ASSETS = os.path.join("src", "TheLongestYear", "assets")

CONTENT = r"C:\Users\Jeff\Documents\Projects\decompiler\stardew-valley-android\content\assets\Content"


def _read_7bit(buf, pos):
    """LEB128 7-bit encoded int. Returns (value, new_pos)."""
    result = 0
    shift = 0
    while True:
        b = buf[pos]
        pos += 1
        result |= (b & 0x7F) << shift
        if (b & 0x80) == 0:
            break
        shift += 7
    return result, pos


def _read_string(buf, pos):
    length, pos = _read_7bit(buf, pos)
    s = buf[pos:pos + length].decode("utf-8")
    return s, pos + length


def decode_xnb_texture(path):
    with open(path, "rb") as f:
        raw = f.read()

    assert raw[0:3] == b"XNB", f"not an XNB: {raw[0:3]}"
    flags = raw[5]
    compressed = bool(flags & 0x40) or bool(flags & 0x80)
    # bytes 6..9 = total file size; if compressed, 10..13 = decompressed size.
    pos = 6
    _total = struct.unpack_from("<I", raw, pos)[0]
    pos += 4
    if compressed:
        decomp_size = struct.unpack_from("<I", raw, pos)[0]
        pos += 4
        data = lz4.block.decompress(raw[pos:], uncompressed_size=decomp_size)
    else:
        data = raw[pos:]

    # --- XNB content: type readers, shared-resource count, then the object. ---
    p = 0
    nreaders, p = _read_7bit(data, p)
    readers = []
    for _ in range(nreaders):
        name, p = _read_string(data, p)
        _ver = struct.unpack_from("<i", data, p)[0]
        p += 4
        readers.append(name)
    nshared, p = _read_7bit(data, p)

    type_id, p = _read_7bit(data, p)  # 1-based index into readers; 0 = null
    reader = readers[type_id - 1]
    print(f"  nreaders={nreaders} readers={readers} nshared={nshared} type_id={type_id}")
    print(f"  header bytes @p={p}: {data[p:p+24].hex(' ')}")
    assert "Texture2DReader" in reader, f"unexpected reader: {reader}"

    surface_format = struct.unpack_from("<i", data, p)[0]; p += 4
    width = struct.unpack_from("<i", data, p)[0]; p += 4
    height = struct.unpack_from("<i", data, p)[0]; p += 4
    mip_count = struct.unpack_from("<i", data, p)[0]; p += 4
    mip0_size = struct.unpack_from("<I", data, p)[0]; p += 4
    pixels = data[p:p + mip0_size]

    # This MonoGame/Android build stores width/height with garbage in the high 16
    # bits; the true dimensions live in the low 16. Mask, then validate against the
    # actual pixel-buffer size so we never silently use wrong dims.
    if width * height * 4 != mip0_size:
        width &= 0xFFFF
        height &= 0xFFFF
    assert surface_format == 0, f"expected Color (0), got {surface_format}"
    assert width * height * 4 == mip0_size, f"dims {width}x{height} != {mip0_size} bytes"
    print(f"  surface_format={surface_format} {width}x{height} mips={mip_count} bytes={mip0_size}")

    img = Image.frombytes("RGBA", (width, height), pixels)
    # Content pipeline premultiplies alpha; un-premultiply back to straight alpha.
    px = img.load()
    for y in range(height):
        for x in range(width):
            r, g, b, a = px[x, y]
            if 0 < a < 255:
                r = min(255, r * 255 // a)
                g = min(255, g * 255 // a)
                b = min(255, b * 255 // a)
                px[x, y] = (r, g, b, a)
    return img


def big_craftable_rect(index, sheet_w):
    x = index * 16 % sheet_w
    y = index * 16 // sheet_w * 32
    return (x, y, x + 16, y + 32)


def recolor_hue(img, hue, sat_scale=0.7, sat_floor=0.20):
    """Set every visible pixel to a fixed hue, keeping its brightness so the
    original sculpt/shading survives. Used to recolor real game sprites."""
    out = img.copy()
    px = out.load()
    for y in range(out.height):
        for x in range(out.width):
            r, g, b, a = px[x, y]
            if a == 0:
                continue
            _, s, v = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
            s = min(1.0, s * sat_scale + sat_floor)
            nr, ng, nb = colorsys.hsv_to_rgb(hue, s, v)
            px[x, y] = (int(nr * 255), int(ng * 255), int(nb * 255), a)
    return out


def recolor_shrine(img):
    """Stone Junimo -> green junimo body with a golden star. The star occupies the
    top of the cell (rows < 17), the junimo body the rest (see the brightness map
    in tools/)."""
    out = img.copy()
    px = out.load()
    for y in range(out.height):
        for x in range(out.width):
            r, g, b, a = px[x, y]
            if a == 0:
                continue
            _, s, v = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
            if y < 17:                                  # star -> bright gold/yellow
                hue, s, v = 0.135, min(1.0, s * 0.35 + 0.70), min(1.0, v * 1.20 + 0.12)
            else:                                       # junimo body -> green
                hue, s = 0.30, min(1.0, s * 0.75 + 0.28)
            nr, ng, nb = colorsys.hsv_to_rgb(hue, s, v)
            px[x, y] = (int(nr * 255), int(ng * 255), int(nb * 255), a)
    return out


def _save(img, asset_name, preview_name):
    img.save(os.path.join(ASSETS, asset_name))
    img.resize((img.width * 12, img.height * 12), Image.NEAREST).save(
        os.path.join("tools", "preview", preview_name))


if __name__ == "__main__":
    sheet = decode_xnb_texture(CONTENT + r"\TileSheets\Craftables.xnb")
    sheet.save("tools/preview/_craftables_full.png")
    print(f"sheet {sheet.size}; saved full sheet")
    for name, idx in [("junimo_chest", 256), ("stone_junimo", 55)]:
        crop = sheet.crop(big_craftable_rect(idx, sheet.width))
        crop.save(f"tools/preview/_{name}.png")
        crop.resize((crop.width * 12, crop.height * 12), Image.NEAREST).save(f"tools/preview/_{name}_big.png")
        print(f"saved {name} (index {idx})")

    # --- Final mod assets: recolor the real sprites ---------------------------
    # Stash chest: the green Junimo Chest, recolored purple. Vanilla draws a chest
    # as base body (256) + a lid overlay (currentLidFrame); the overlay frames
    # (257..261) have NO body of their own, so we composite each over the 256 body
    # to get five complete frames (closed -> fully open). The draw patch then picks
    # the frame matching the chest's currentLidFrame.
    CHEST_OVERLAYS = list(range(257, 262))   # closed -> fully open
    base = sheet.crop(big_craftable_rect(256, sheet.width))
    strip = Image.new("RGBA", (16 * len(CHEST_OVERLAYS), 32), (0, 0, 0, 0))
    for i, idx in enumerate(CHEST_OVERLAYS):
        frame = base.copy()
        frame.alpha_composite(sheet.crop(big_craftable_rect(idx, sheet.width)))
        strip.paste(frame, (i * 16, 0))
    _save(recolor_hue(strip, hue=0.78, sat_scale=0.85, sat_floor=0.22),
          "junimo_stash.png", "junimo_stash_big.png")
    # Planning shrine: the Stone Junimo — green body, golden star.
    junimo = sheet.crop(big_craftable_rect(55, sheet.width))
    _save(recolor_shrine(junimo), "shrine.png", "shrine_big.png")
    print(f"wrote junimo_stash.png ({len(CHEST_OVERLAYS)} frames) + shrine.png to assets")
