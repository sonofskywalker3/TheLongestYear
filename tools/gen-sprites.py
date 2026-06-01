"""Generate placeholder sprites for The Longest Year's placeable interactables.

Run once from the repo root:  python tools/gen-sprites.py
Writes:
  src/TheLongestYear/assets/books.png  (48x16: three 16x16 book covers, sprite indices 0/1/2)
  src/TheLongestYear/assets/shrine.png (16x32: a small stone shrine with a junimo glow)
Not shipped; the PNGs it produces are.
"""
import os
from PIL import Image

OUT = os.path.join("src", "TheLongestYear", "assets")
os.makedirs(OUT, exist_ok=True)

# Three 16x16 book covers in one 48x16 tilesheet: red, blue, green spines.
books = Image.new("RGBA", (48, 16), (0, 0, 0, 0))
covers = [(150, 40, 40), (40, 70, 150), (40, 120, 60)]
for i, c in enumerate(covers):
    ox = i * 16
    for x in range(2, 14):
        for y in range(2, 15):
            books.putpixel((ox + x, y), (*c, 255))
    for y in range(2, 15):                      # spine highlight
        books.putpixel((ox + 3, y), (255, 255, 255, 255))
    for x in range(2, 14):                      # page edge
        books.putpixel((ox + x, 14), (235, 225, 200, 255))
books.save(os.path.join(OUT, "books.png"))

# Shrine: 16x32 little stone shrine with a green junimo glow alcove.
shrine = Image.new("RGBA", (16, 32), (0, 0, 0, 0))
for x in range(3, 13):
    for y in range(14, 30):
        shrine.putpixel((x, y), (110, 110, 120, 255))   # stone body
for x in range(5, 11):
    for y in range(8, 14):
        shrine.putpixel((x, y), (70, 160, 70, 255))      # junimo glow alcove
shrine.save(os.path.join(OUT, "shrine.png"))

print("wrote books.png + shrine.png")
