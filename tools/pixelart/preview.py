"""Checks game/Assets/Ekqo/Game/Quest/Resources/PixelArt.txt and Garden.txt and draws previews.

    python3 tools/pixelart/preview.py [out_dir]

Writes sprites.png (every sprite) and garden.png (the Quest 1 map at night with the child,
Ekqo and his bush placed as in the game). Needs Pillow (pip install pillow).
"""
import os, sys
from PIL import Image

ROOT = os.path.join(os.path.dirname(__file__), "..", "..", "game", "Assets", "Ekqo", "Game", "Quest", "Resources")
TILE = 32
NIGHT = (0.50, 0.56, 0.86)    # same tint as PixelArt.NightTint
CHARACTER = tuple(1 + (t - 1) * 0.45 for t in NIGHT)   # Night.Character
LIT = {"window", "lantern"}   # their light (colours y and Y) stays bright at night
LIGHT = "yY"


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
                assert len({len(r) for r in rows}) == 1, f"{name}: rows differ in length"
            cur = None; continue
        if cur == "palette":
            k, v = line.split(); palette[k] = tuple(int(v[i:i + 2], 16) for i in (0, 2, 4))
        elif cur == "sprite":
            for ch in line:
                assert ch == "." or ch in palette, f"line {n} ({name}): unknown colour {ch!r}"
            sprites[name].append(line)
    return palette, sprites


def image(palette, rows, tint=None, keep=""):
    img = Image.new("RGBA", (len(rows[0]), len(rows)), (0, 0, 0, 0))
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            if ch == ".":
                continue
            c = palette[ch]
            if tint and ch not in keep:
                c = tuple(int(v * t) for v, t in zip(c, tint))
            img.putpixel((x, y), c + (255,))
    return img


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
            name, x, y = line[1:].split(); marks[name] = (int(x) * TILE, int(y) * TILE)
        else:
            rows.append(line)
    assert len({len(r) for r in rows}) == 1, "map rows differ in length"
    return legend, rows, marks


def ground(palette, sprites):
    legend, rows, marks = load_map(os.path.join(ROOT, "Garden.txt"))
    base = Image.new("RGBA", (len(rows[0]) * TILE, len(rows) * TILE))
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            name = legend[ch]; assert name in sprites, f"map uses unknown sprite {name}"
            base.alpha_composite(image(palette, sprites["grass"], NIGHT), (x * TILE, y * TILE))
            if name != "grass":
                base.alpha_composite(image(palette, sprites[name], NIGHT, LIGHT if name in LIT else ""), (x * TILE, y * TILE))
    return base, marks


def halo(size, strength):
    from PIL import ImageDraw
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    c = size / 2
    for i in range(6, 0, -1):
        k = i / 6; r = size * 0.22 * (1 + k * 1.4)
        lay = Image.new("RGBA", (size, size), (0, 0, 0, 0))
        ImageDraw.Draw(lay).ellipse((c - r, c - r, c + r, c + r), fill=(255, 235, 140, int(255 * strength * 0.12 * (1.2 - k))))
        img.alpha_composite(lay)
    return img


def garden(palette, sprites, ekqo_out=True, ekqo="ekqo"):
    """The map with the characters placed like QuestPlayer does (same offsets)."""
    g, marks = ground(palette, sprites)
    bx, by = marks["ekqo_bush"]
    cx, cy = marks["player_stop"]
    g.alpha_composite(image(palette, sprites["child_side"], CHARACTER), (cx, cy))
    ew = len(sprites[ekqo][0])
    ex, ey = bx + 6, by + (-30 if ekqo_out else 8)
    g.alpha_composite(halo(100, 0.35 + 1.3 * (0.8 if ekqo_out else 0.12)), (ex + ew // 2 - 50, ey + ew // 2 - 50))
    g.alpha_composite(image(palette, sprites[ekqo]), (ex, ey))
    g.alpha_composite(image(palette, sprites["bush_big"], NIGHT), (bx, by))
    return g, marks


def main():
    out = sys.argv[1] if len(sys.argv) > 1 else "."
    palette, sprites = load(os.path.join(ROOT, "PixelArt.txt"))
    names = list(sprites)
    cols, cell = 8, 52
    sheet = Image.new("RGBA", (cols * cell, ((len(names) + cols - 1) // cols) * cell), (40, 40, 60, 255))
    for i, n in enumerate(names):
        im = image(palette, sprites[n])
        sheet.alpha_composite(im, ((i % cols) * cell + (cell - im.width) // 2, (i // cols) * cell + (cell - im.height) // 2))
    sheet.resize((sheet.width * 3, sheet.height * 3), Image.NEAREST).save(os.path.join(out, "sprites.png"))
    g, _ = garden(palette, sprites)
    g.resize((g.width * 2, g.height * 2), Image.NEAREST).save(os.path.join(out, "garden.png"))
    print(f"{len(names)} sprites OK: {', '.join(names)}")


if __name__ == "__main__":
    main()
