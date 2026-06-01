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

    cover_x0 = BX0 + SPINE_W       # 5
    cover_x1 = BX1 - PAGE_W        # 11

    # Cover face.
    bfill(cover_x0, cover_x1, BY0, BY1, base)

    # Spine binding on the left, with a hinge highlight line beside the cover.
    bfill(BX0, BX0 + SPINE_W - 1, BY0, BY1, spine)
    for y in range(BY0, BY1 + 1):
        bput(BX0 + SPINE_W - 1, y, spine_hi)

    # Fore-edge: stacked pages on the right, with a shaded bottom for thickness.
    bfill(BX1 - PAGE_W + 1, BX1, BY0 + 1, BY1 - 1, PAGE)
    for x in range(BX1 - PAGE_W + 1, BX1 + 1):
        bput(x, BY1 - 1, PAGE_SHADOW)

    # Title bands on the cover.
    for ty in (BY0 + 3, BY0 + 6):
        bfill(cover_x0 + 1, cover_x1 - 1, ty, ty, light)

    # Dark outline around the whole book.
    for x in range(BX0, BX1 + 1):
        bput(x, BY0, dark)
        bput(x, BY1, dark)
    for y in range(BY0, BY1 + 1):
        bput(BX0, y, dark)
        bput(BX1, y, dark)

books.save(os.path.join(OUT, "books.png"))
save_preview(books, "books_preview.png")

# ---------------------------------------------------------------------------
# Shrine: 16x32. A green junimo statue (the classic round body + big dark eyes +
# leaf sprout) sitting on a small stone altar. The junimo occupies the upper
# 16px tile; the altar is the lower (footprint) tile.
# ---------------------------------------------------------------------------
STONE = (138, 132, 120)
STONE_DK = (96, 90, 80)
STONE_LT = (176, 170, 156)
JUNI = (108, 196, 78)
JUNI_DK = (60, 132, 52)
JUNI_HI = (170, 226, 120)
EYE = (28, 30, 30)
EYE_HI = (235, 240, 235)
LEAF = (86, 178, 70)

shrine = Image.new("RGBA", (16, 32), TRANSPARENT)


def sput(x, y, c):
    put(shrine, x, y, c)


def sfill(x0, x1, y0, y1, c):
    fill(shrine, x0, x1, y0, y1, c)


# --- Stone altar (lower tile) -------------------------------------------------
# Pillar.
sfill(4, 11, 17, 25, STONE)
# Top slab the junimo sits on.
sfill(3, 12, 15, 16, STONE_LT)
sfill(3, 12, 17, 17, STONE)
# Stepped base.
sfill(3, 12, 26, 27, STONE_DK)
sfill(2, 13, 28, 29, STONE)
sfill(2, 13, 30, 30, STONE_DK)
# Side shading for volume.
for y in range(17, 26):
    sput(4, y, STONE_DK)
    sput(11, y, STONE_LT)
# A couple of carved seams.
for y in (19, 22):
    sfill(5, 10, y, y, STONE_DK)

# --- Junimo statue (upper tile) ----------------------------------------------
# Rounded dome body: per-row x-spans (narrow at top, full width, flat bottom).
BODY_ROWS = {
    2: (7, 8),
    3: (6, 9),
    4: (5, 10),
    5: (4, 11),
    6: (4, 11),
    7: (3, 12),
    8: (3, 12),
    9: (3, 12),
    10: (3, 12),
    11: (3, 12),
    12: (3, 12),
    13: (4, 11),
    14: (4, 11),
}
for y, (x0, x1) in BODY_ROWS.items():
    sfill(x0, x1, y, y, JUNI)

# Outline (darker green) around the body edge + bottom shadow.
for y, (x0, x1) in BODY_ROWS.items():
    sput(x0, y, JUNI_DK)
    sput(x1, y, JUNI_DK)
sfill(4, 11, 14, 14, JUNI_DK)         # grounded bottom edge
for x in range(7, 9):                  # top cap outline
    sput(x, 1, JUNI_DK)

# Highlight on the upper-left for roundness.
sfill(5, 6, 4, 5, JUNI_HI)
sput(4, 6, JUNI_HI)

# Leaf sprout on top.
sput(8, 0, LEAF)
sput(7, 1, LEAF)
sput(9, 1, JUNI_DK)

# Big dark eyes with a highlight glint.
for ex in (5, 9):
    sfill(ex, ex + 1, 8, 10, EYE)
    sput(ex, 8, EYE_HI)

shrine.save(os.path.join(OUT, "shrine.png"))
save_preview(shrine, "shrine_preview.png")

print("wrote books.png + shrine.png (+ previews in tools/preview/)")
