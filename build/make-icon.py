"""Draws the Satsuma app icon (a segmented orange slice with a leaf) and writes Assets/Satsuma.ico.

Run from the repository root: python3 build/make-icon.py
"""
import math
from PIL import Image, ImageDraw

SIZE = 1024
img = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
d = ImageDraw.Draw(img)

cx, cy, r = SIZE / 2, SIZE / 2 + 40, 400
# Peel and pith.
d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=(242, 118, 22, 255))
pith = r - 46
d.ellipse([cx - pith, cy - pith, cx + pith, cy + pith], fill=(255, 236, 205, 255))
# Eight segments, like the wheel the app shows around the cursor.
flesh = pith - 22
for i in range(8):
    start = i * 45 - 90 + 4
    d.pieslice([cx - flesh, cy - flesh, cx + flesh, cy + flesh], start, start + 37, fill=(255, 149, 38, 255))
hub = 58
d.ellipse([cx - hub, cy - hub, cx + hub, cy + hub], fill=(255, 236, 205, 255))
# Leaf.
leaf = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
ld = ImageDraw.Draw(leaf)
ld.ellipse([cx - 40, 60, cx + 230, 210], fill=(70, 160, 70, 255))
leaf = leaf.rotate(-28, center=(cx + 40, 150), resample=Image.BICUBIC)
img.alpha_composite(leaf)

img.save("src/Satsuma.App/Assets/Satsuma.ico", sizes=[(s, s) for s in (16, 20, 24, 32, 40, 48, 64, 128, 256)])
img.resize((256, 256), Image.LANCZOS).save("build/icon-preview.png")
