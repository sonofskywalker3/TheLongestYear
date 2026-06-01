"""Generate sprites for The Longest Year's placeable interactables.

Run once from the repo root:  python tools/gen-sprites.py
Writes:
  src/TheLongestYear/assets/books.png  (48x16: three 16x16 book covers, sprite indices 0/1/2)
  src/TheLongestYear/assets/shrine.png (16x32: a small stone shrine with a junimo glow)
Also writes zoomed previews under tools/preview/ for eyeballing (NOT shipped).
"""
import os
from PIL import Image

OUT = os.path.join("src", "TheLongestYear", "assets")
PREVIEW = os.path.join("tools", "preview")
os.makedirs(OUT, exist_ok=True)
os.makedirs(PREVIEW, exist_ok=True)

TRANSPARENT = (0, 0, 0, 0)


def shade(c, f):
    return tuple(min(255, max(0, int(v * f))) for v in c)


def put(img, x, y, c):
    if len(c) == 3:
        c = (*c, 255)
    img.putpixel((x, y), c)


def fill(img, x0, x1, y0, y1, c):
    for x in range(x0, x1 + 1):
        for y in range(y0, y1 + 1):
            put(img, x, y, c)


def save_preview(img, name, scale=16):
    big = img.resize((img.width * scale, img.height * scale), Image.NEAREST)
    big.save(os.path.join(PREVIEW, name))


# ---------------------------------------------------------------------------
# Books: three 16x16 closed hardcover books, front-on with the spine binding on
# the left and a cream fore-edge (page stack) on the right. A couple of light
# title bands sit on the cover so it reads as a book, not a flat tile.
# ---------------------------------------------------------------------------
PAGE = (246, 238, 208)
PAGE_SHADOW = (206, 194, 160)

# Cover base colours: warm red, ink blue, leaf green (Cook / Craft / Bundle-log).
COVERS = [(168, 54, 48), (46, 78, 156), (52, 124, 66)]

books = Image.new("RGBA", (48, 16), TRANSPARENT)

# Book body: x 2..13 (12 wide), y 2..13 (12 tall). Spine 3 cols, pages 2 cols.
BX0, BX1, BY0, BY1 = 2, 13, 2, 13
SPINE_W = 3          # left binding columns
PAGE_W = 2           # right fore-edge columns

for i, base in enumerate(COVERS):
    ox = i * 16
    dark = shade(base, 0.40)
    spine = shade(base, 0.68)
    spine_hi = shade(base, 0.85)
    light = shade(base, 1.55)

    def bput(x, y, c):
        put(books, ox + x, y, c)

    def bfill(x0, x1, y0, y1, c):
        for x in range(x0, x1 + 1):
            for y in range(y0, y1 + 1):
                put(books, ox + x, y, c)

    # A closed hardcover seen front-on: full cover, a spine binding on the left
    # (with raised cords), a framed title plate, and a thin sliver of closed pages
    # along the bottom. No open-edge so it reads as shut, not ajar.
    spine_x1 = BX0 + SPINE_W - 1    # 4

    # Cover face fills the whole book; the spine then overpaints the left columns.
    bfill(BX0, BX1, BY0, BY1, base)
    bfill(BX0, spine_x1, BY0, BY1, spine)
    for y in range(BY0, BY1 + 1):                 # hinge highlight beside the cover
        bput(spine_x1, y, spine_hi)
    for cy in (BY0 + 3, BY0 + 8):                 # raised cords across the spine
        bfill(BX0, spine_x1, cy, cy, dark)

    # Framed title plate centred on the cover.
    bfill(spine_x1 + 2, BX1 - 1, BY0 + 3, BY0 + 7, light)
    bfill(spine_x1 + 3, BX1 - 2, BY0 + 4, BY0 + 6, base)

    # Thin closed-page sliver along the bottom of the cover.
    bfill(spine_x1 + 1, BX1 - 1, BY1 - 1, BY1 - 1, PAGE)

    # Dark outline around the whole book.
    for x in range(BX0, BX1 + 1):
        bput(x, BY0, dark)
        bput(x, BY1, dark)
    for y in range(BY0, BY1 + 1):
        bput(BX0, y, dark)
        bput(BX1, y, dark)

books.save(os.path.join(OUT, "books.png"))
save_preview(books, "books_preview.png")

# Note: shrine.png is no longer generated here — it's a recolored copy of the real
# Stone Junimo sprite, produced by tools/extract_sprites.py. Likewise the stash
# chest sprite (junimo_stash.png). This script now owns only the book covers.

print("wrote books.png (+ preview in tools/preview/)")
