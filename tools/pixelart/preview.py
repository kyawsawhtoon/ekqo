"""Checks game/Assets/Ekqo/Game/Quest/Resources/PixelArt.txt and draws previews.

    python3 tools/pixelart/preview.py [out_dir]

Writes sprites.png (every sprite, 8x) and garden.png (the Quest 1 map from Garden.txt).
Needs Pillow (pip install pillow).
"""
import os, sys
from PIL import Image

ROOT = os.path.join(os.path.dirname(__file__), "..", "..", "game", "Assets", "Ekqo", "Game", "Quest", "Resources")

def load(path):
    palette, sprites, cur, name = {}, {}, None, None
    for n, raw in enumerate(open(path, encoding="utf-8"), 1):
        line = raw.rstrip("\n")
        if not line or line.startswith("#"):
            continue
        if line == "palette":
            cur = "palette"; continue
        if line.startswith("sprite "):
            name = line.split()[1]; sprites[name] = []; cur = "sprite"; continue
        if line == "end":
            if cur == "sprite":
                rows = sprites[name]
                assert len(rows) == 16, f"{name}: {len(rows)} rows"
            cur = None; continue
        if cur == "palette":
            k, v = line.split(); palette[k] = tuple(int(v[i:i+2], 16) for i in (0, 2, 4))
        elif cur == "sprite":
            assert len(line) == 16, f"line {n} ({name}): {len(line)} columns"
            for ch in line:
                assert ch == "." or ch in palette, f"line {n} ({name}): unknown colour {ch!r}"
            sprites[name].append(line)
    return palette, sprites

def image(palette, rows):
    img = Image.new("RGBA", (16, 16), (0, 0, 0, 0))
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            if ch != ".":
                img.putpixel((x, y), palette[ch] + (255,))
    return img

NIGHT = (0.50, 0.56, 0.86)  # same tint as PixelArt.cs NightTint
LIT = {"window", "lantern"}   # their light (colours y and Y) stays bright at night
LIGHT = "yY"

def load_map(path):
    legend, rows, marks = {}, [], {}
    for raw in open(path, encoding="utf-8"):
        line = raw.rstrip("\n")
        if not line or line.startswith("#"):
            continue
        if line.startswith("legend "):
            for kv in line.split()[1:]:
                k, v = kv.split("="); legend[k] = v
        elif line.startswith("@"):
            name, x, y = line[1:].split(); marks[name] = (int(x), int(y))
        else:
            rows.append(line)
    assert len({len(r) for r in rows}) == 1, "map rows differ in length"
    return legend, rows, marks

def tint(img, k):
    px = img.load()
    for y in range(img.height):
        for x in range(img.width):
            r, g, b, a = px[x, y]
            px[x, y] = (int(r * k[0]), int(g * k[1]), int(b * k[2]), a)
    return img

def garden(palette, sprites):
    legend, rows, marks = load_map(os.path.join(ROOT, "Garden.txt"))
    w, h = len(rows[0]) * 16, len(rows) * 16
    base = Image.new("RGBA", (w, h)); lit = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            name = legend[ch]; assert name in sprites, f"map uses unknown sprite {name}"
            if name not in ("grass",):  # non-grass tiles sit on grass
                base.alpha_composite(image(palette, sprites["grass"]), (x * 16, y * 16))
            rows_ = sprites[name]
            base.alpha_composite(image(palette, rows_), (x * 16, y * 16))
            if name in LIT:
                glow = [''.join(c if c in LIGHT else "." for c in r) for r in rows_]
                lit.alpha_composite(image(palette, glow), (x * 16, y * 16))
    tint(base, NIGHT)
    base.alpha_composite(lit)
    # Quest 1 opening: the child by the bush, Ekqo glowing out of it.
    bx, by = marks["ekqo_bush"]; cx, cy = marks["player_stop"]
    big = image(palette, sprites["bush"]).resize((32, 32), Image.NEAREST)
    base.alpha_composite(image(palette, sprites["child_up"]), (cx * 16, cy * 16))
    glow = Image.new("RGBA", (40, 40), (0, 0, 0, 0))
    for r in range(20, 0, -2):
        a = int(60 * (1 - r / 20)) + 10
        for yy in range(40):
            for xx in range(40):
                if (xx - 20) ** 2 + (yy - 20) ** 2 <= r * r:
                    glow.putpixel((xx, yy), (255, 230, 140, min(255, a)))
    base.alpha_composite(glow, (bx * 16 - 4, by * 16 - 22))
    base.alpha_composite(image(palette, sprites["ekqo"]), (bx * 16 + 8, by * 16 - 14))
    base.alpha_composite(tint(big, NIGHT), (bx * 16, by * 16))
    return base

def main():
    out = sys.argv[1] if len(sys.argv) > 1 else "."
    palette, sprites = load(os.path.join(ROOT, "PixelArt.txt"))
    names = list(sprites)
    cols = 8
    sheet = Image.new("RGBA", (cols * 18, ((len(names) + cols - 1) // cols) * 18), (40, 40, 60, 255))
    for i, n in enumerate(names):
        sheet.paste(image(palette, sprites[n]), ((i % cols) * 18 + 1, (i // cols) * 18 + 1))
    sheet.resize((sheet.width * 6, sheet.height * 6), Image.NEAREST).save(os.path.join(out, "sprites.png"))
    g = garden(palette, sprites)
    g.resize((g.width * 4, g.height * 4), Image.NEAREST).save(os.path.join(out, "garden.png"))
    print(f"{len(names)} sprites OK: {', '.join(names)}")

if __name__ == "__main__":
    main()
