"""Draws Ekqo's pixel art (32px tiles) and writes it to PixelArt.txt.

    python3 tools/pixelart/generate.py

The art is built from simple shapes (ellipses, rectangles, lines), then outlined and
shaded automatically, so it stays consistent and easy to change. The result is plain text
(one palette letter per pixel) that the game reads at start-up. Small fixes can be made in
PixelArt.txt by hand, but running this script again replaces that file.
All art here is original to Ekqo.
"""
import os, random

OUT = os.path.join(os.path.dirname(__file__), "..", "..", "game", "Assets", "Ekqo", "Game", "Quest", "Resources", "PixelArt.txt")

PALETTE = [
    ("k", "1b1a2c", "outline"), ("K", "3b3652", "soft outline"),
    ("g", "5fbf55", "grass"), ("G", "3f9b46", "grass dark"), ("x", "2c7a3c", "grass darkest"), ("l", "9fe27c", "grass light"),
    ("p", "e3c38c", "path"), ("P", "c4a06a", "path dark"), ("z", "f2dcae", "path light"),
    ("w", "f4e7ca", "wall"), ("W", "cbb08c", "wall dark"), ("i", "fffaf0", "wall light"),
    ("r", "d65e4d", "roof"), ("R", "9c3a38", "roof dark"), ("q", "ee8a6c", "roof light"),
    ("o", "a36b40", "wood"), ("O", "6b442b", "wood dark"), ("j", "c98c58", "wood light"),
    ("y", "ffe27a", "light"), ("Y", "ffb347", "light deep"), ("F", "ffffff", "white"),
    ("b", "5b8fd9", "blue"), ("B", "3a5fa8", "blue dark"), ("c", "a8dcff", "blue light"),
    ("f", "ff86ad", "pink"), ("M", "c94f75", "pink dark"),
    ("s", "ffd9b3", "skin"), ("S", "e0a87c", "skin dark"),
    ("h", "6a3c26", "hair"), ("H", "43251a", "hair dark"), ("u", "8c5636", "hair light"),
    ("t", "3fb39a", "shirt"), ("T", "257a66", "shirt dark"), ("v", "7fd9c2", "shirt light"),
    ("n", "8a6ae0", "purple"), ("N", "5b43b0", "purple dark"),
    ("e", "fffbe8", "Ekqo"), ("E", "ffe08a", "Ekqo shade"), ("m", "ff9a9a", "cheek"),
    ("a", "b4b4cc", "stone"), ("A", "7c7c98", "stone dark"),
    ("d", "4e9a40", "leaf"), ("D", "2f6a2c", "leaf dark"), ("L", "86cc5c", "leaf light"),
    ("U", "d9d2b4", "Ekqo dim"), ("V", "a8a088", "Ekqo dim shade"),
    ("C", "e8505b", "red"), ("Q", "3fae5a", "green"),
]
HEX = {k: v for k, v, _ in PALETTE}


class Art:
    def __init__(self, w=32, h=None, fill="."):
        self.w, self.h = w, h or w
        self.px = [[fill] * self.w for _ in range(self.h)]

    def get(self, x, y):
        return self.px[y][x] if 0 <= x < self.w and 0 <= y < self.h else "."

    def set(self, x, y, c):
        if 0 <= x < self.w and 0 <= y < self.h:
            self.px[y][x] = c

    def rect(self, x0, y0, x1, y1, c):
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                self.set(x, y, c)

    def ellipse(self, cx, cy, rx, ry, c):
        for y in range(int(cy - ry - 1), int(cy + ry + 2)):
            for x in range(int(cx - rx - 1), int(cx + rx + 2)):
                if ((x + 0.5 - cx) / rx) ** 2 + ((y + 0.5 - cy) / ry) ** 2 <= 1:
                    self.set(x, y, c)

    def line(self, x0, y0, x1, y1, c):
        n = max(abs(x1 - x0), abs(y1 - y0), 1)
        for i in range(n + 1):
            self.set(round(x0 + (x1 - x0) * i / n), round(y0 + (y1 - y0) * i / n), c)

    def poly(self, pts, c):
        ys = [p[1] for p in pts]
        for y in range(min(ys), max(ys) + 1):
            xs = []
            for i in range(len(pts)):
                (x0, y0), (x1, y1) = pts[i], pts[(i + 1) % len(pts)]
                if (y0 <= y + 0.5 < y1) or (y1 <= y + 0.5 < y0):
                    xs.append(x0 + (y + 0.5 - y0) * (x1 - x0) / (y1 - y0))
            xs.sort()
            for a, b in zip(xs[::2], xs[1::2]):
                for x in range(round(a), round(b)):
                    self.set(x, y, c)

    def outline(self, c="k", inside=None):
        """Draws an outline around everything opaque (on the transparent pixels next to it)."""
        add = []
        for y in range(self.h):
            for x in range(self.w):
                if self.px[y][x] != ".":
                    continue
                if any(self.get(x + dx, y + dy) != "." for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                    add.append((x, y))
        for x, y in add:
            self.set(x, y, c)

    def shade(self, base, dark, light=None, depth=2):
        """Darkens the lower-right edge of a colour area and lights the upper-left edge."""
        src = [row[:] for row in self.px]
        g = lambda x, y: src[y][x] if 0 <= x < self.w and 0 <= y < self.h else "."
        for y in range(self.h):
            for x in range(self.w):
                if src[y][x] != base:
                    continue
                if any(g(x + d, y + d) != base for d in range(1, depth + 1)) or g(x, y + depth) != base:
                    self.px[y][x] = dark
                elif light and (g(x - 1, y - 1) != base or g(x, y - 1) != base):
                    self.px[y][x] = light

    def replace(self, a, b):
        for y in range(self.h):
            for x in range(self.w):
                if self.px[y][x] == a:
                    self.px[y][x] = b

    def paste(self, other, ox, oy):
        for y in range(other.h):
            for x in range(other.w):
                if other.px[y][x] != ".":
                    self.set(ox + x, oy + y, other.px[y][x])

    def mirror(self):
        a = Art(self.w, self.h)
        a.px = [row[::-1] for row in self.px]
        return a

    def rows(self):
        return ["".join(r) for r in self.px]


SPRITES = {}


def sprite(name, art):
    SPRITES[name] = art
    return art


# ---------------------------------------------------------------- tiles

def grass(seed, tufts=10, flowers=0):
    rng = random.Random(seed)
    a = Art(32, fill="g")
    for _ in range(40):
        a.set(rng.randrange(32), rng.randrange(32), "G")
    for _ in range(tufts):
        x, y = rng.randrange(2, 30), rng.randrange(3, 30)
        a.set(x, y, "x"); a.set(x - 1, y - 1, "G"); a.set(x + 1, y - 1, "G"); a.set(x, y - 2, "l"); a.set(x - 2, y - 2, "G")
    for _ in range(flowers):
        x, y = rng.randrange(3, 29), rng.randrange(3, 29)
        petal = rng.choice("fFny")
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1), (1, -1), (-1, -1), (1, 1), (-1, 1)):
            a.set(x + dx, y + dy, petal)
        a.set(x, y, "Y" if petal == "y" else "y"); a.set(x, y + 2, "x"); a.set(x, y + 3, "x"); a.set(x + 1, y + 3, "G")
    return a


sprite("grass", grass(1))
sprite("grass2", grass(2, tufts=18))
sprite("flowers", grass(3, tufts=6, flowers=5))


def path():
    rng = random.Random(9)
    a = Art(32, fill="p")
    for _ in range(46):
        a.set(rng.randrange(32), rng.randrange(32), rng.choice("PPz"))
    for _ in range(4):
        x, y = rng.randrange(2, 29), rng.randrange(2, 29)
        a.set(x, y, "P"); a.set(x + 1, y, "P"); a.set(x, y - 1, "z")
    return a


sprite("path", path())


def stone():
    a = path()
    for cx, cy, rx, ry in ((9, 9, 6, 4.5), (22, 15, 6.5, 4.5), (11, 24, 6, 4.5)):
        s = Art(32)
        s.ellipse(cx, cy, rx, ry, "a")
        s.shade("a", "A", "F", 1)
        s.outline("K")
        a.paste(s, 0, 0)
    return a


sprite("stone", stone())


def roof():
    a = Art(32, fill="r")
    for band in range(4):
        y0 = band * 8
        a.rect(0, y0 + 6, 31, y0 + 7, "R")
        a.rect(0, y0, 31, y0, "q")
        off = 0 if band % 2 == 0 else 8
        for x in range(off, 32, 16):
            a.rect(x, y0, x, y0 + 5, "R")
    return a


sprite("roof", roof())


def wall(base=False):
    a = Art(32, fill="w")
    for y0 in range(0, 32, 8):
        a.rect(0, y0 + 7, 31, y0 + 7, "W")
        a.rect(0, y0, 31, y0, "i")
    if base:
        a.rect(0, 26, 31, 29, "W")
        a.rect(0, 30, 31, 31, "k")
    return a


sprite("wall", wall())
sprite("wall_low", wall(True))


def window():
    a = wall()
    a.rect(4, 4, 27, 25, "k")
    a.rect(5, 5, 26, 24, "O")
    a.rect(7, 7, 24, 22, "y")
    a.rect(7, 16, 24, 22, "Y")
    a.rect(15, 7, 16, 22, "O")
    a.rect(7, 14, 24, 15, "O")
    for x, y in ((9, 9), (10, 9), (9, 10), (19, 9), (20, 9), (19, 10)):
        a.set(x, y, "F")
    a.rect(3, 26, 28, 28, "j")
    a.rect(3, 28, 28, 28, "O")
    a.rect(3, 29, 28, 29, "k")
    return a


sprite("window", window())


def door():
    a = wall(True)
    a.rect(7, 2, 24, 29, "k")
    a.rect(8, 3, 23, 29, "o")
    for x in (12, 16, 20):
        a.rect(x, 3, x, 29, "O")
    a.rect(8, 3, 23, 3, "j")
    a.rect(20, 16, 21, 17, "y")
    a.rect(4, 30, 27, 31, "P")
    return a


sprite("door", door())


def fence():
    a = Art(32)
    for x0 in (3, 19):
        a.rect(x0, 6, x0 + 5, 28, "o")
        a.rect(x0, 6, x0, 28, "j")
        a.rect(x0 + 5, 6, x0 + 5, 28, "O")
    a.rect(0, 11, 31, 14, "o")
    a.rect(0, 20, 31, 23, "o")
    a.rect(0, 11, 31, 11, "j")
    a.rect(0, 20, 31, 20, "j")
    a.rect(0, 14, 31, 14, "O")
    a.rect(0, 23, 31, 23, "O")
    for x0 in (3, 19):
        a.rect(x0 + 1, 6, x0 + 4, 10, "o"); a.rect(x0 + 1, 15, x0 + 4, 19, "o"); a.rect(x0 + 1, 24, x0 + 4, 28, "o")
        a.rect(x0, 6, x0, 28, "j"); a.rect(x0 + 5, 6, x0 + 5, 28, "O")
    a.outline("k")
    return a


sprite("fence", fence())


def tree():
    a = Art(32)
    a.rect(13, 21, 18, 29, "o")
    a.rect(13, 21, 13, 29, "j")
    a.rect(18, 21, 18, 29, "O")
    c = Art(32)
    for cx, cy, r in ((16, 13, 11), (9, 16, 6.5), (23, 16, 6.5), (16, 7, 7.5)):
        c.ellipse(cx, cy, r, r * 0.92, "d")
    c.shade("d", "D", "L", 3)
    rng = random.Random(4)
    for _ in range(9):
        x, y = rng.randrange(8, 24), rng.randrange(5, 20)
        if c.get(x, y) == "d":
            c.set(x, y, "L"); c.set(x + 1, y + 1, "D")
    a.paste(c, 0, 0)
    a.outline("k")
    return a


sprite("tree", tree())


def bush(size=32, seed=5):
    a = Art(size)
    s = size / 32
    for cx, cy, rx, ry in ((16, 19, 13, 9), (10, 15, 7, 6), (21, 14, 8, 7), (16, 12, 6, 5)):
        a.ellipse(cx * s, cy * s, rx * s, ry * s, "d")
    a.shade("d", "D", "L", max(2, round(2 * s)))
    rng = random.Random(seed)
    for _ in range(int(6 * s * s)):
        x, y = rng.randrange(int(6 * s), int(26 * s)), rng.randrange(int(9 * s), int(24 * s))
        if a.get(x, y) == "d" and a.get(x + 1, y) == "d":
            a.set(x, y, "f"); a.set(x + 1, y, "M")
    a.outline("k")
    return a


sprite("bush", bush())
sprite("bush_big", bush(56, 6))


def lantern():
    a = Art(32)
    a.rect(14, 12, 17, 29, "A")
    a.rect(14, 12, 14, 29, "a")
    a.rect(11, 28, 20, 30, "A")
    a.rect(10, 2, 21, 3, "A")
    a.rect(11, 4, 20, 11, "y")
    a.rect(11, 8, 20, 11, "Y")
    a.rect(13, 5, 14, 6, "F")
    a.rect(15, 4, 16, 11, "A")
    a.outline("k")
    return a


sprite("lantern", lantern())


# ---------------------------------------------------------------- the child (chibi: big head, small body)

def eye(a, x, y, h=4, shine=True):
    """A big shiny eye, 2 wide."""
    a.rect(x, y, x + 1, y + h - 1, "k")
    a.rect(x, y + h - 2, x + 1, y + h - 1, "H")
    if shine:
        a.set(x, y, "F")


def child(facing, step):
    a = Art(32)
    side = facing == "side"
    # legs and shoes
    if side:
        if step:
            a.rect(11, 25, 13, 28, "b"); a.rect(18, 25, 20, 28, "b")
            a.rect(9, 28, 13, 29, "C"); a.rect(18, 28, 22, 29, "C")
        else:
            a.rect(14, 25, 17, 28, "b"); a.rect(14, 28, 19, 29, "C")
    else:
        lift = 1 if step else 0
        a.rect(11, 25, 14, 28 - lift, "b"); a.rect(17, 25, 20, 28, "b")
        a.rect(10, 28 - lift, 14, 29 - lift, "C"); a.rect(17, 28, 21, 29, "C")
    # body: a small rounded shirt
    a.ellipse(16, 23, 6.5, 4.5, "t")
    a.rect(10, 20, 21, 25, "t")
    if side:
        sw = 2 if step else 0
        a.rect(15 + sw, 21, 17 + sw, 24, "t")
        a.rect(15 + sw, 25, 17 + sw, 25, "s")
    else:
        sw = 1 if step else 0
        a.rect(7, 21 + sw, 9, 24 + sw, "t"); a.rect(22, 22 - sw, 24, 25 - sw, "t")
        a.rect(7, 25 + sw, 9, 25 + sw, "s"); a.rect(22, 26 - sw, 24, 26 - sw, "s")
        if facing == "down":
            a.ellipse(16, 23, 2, 1.5, "y")   # a little star badge on the shirt
    # head: big and round
    a.ellipse(16, 11.5, 11, 10, "s")
    if facing == "up":
        a.ellipse(16, 11, 11, 10, "h")
        a.rect(15, 0, 16, 1, "h")
        a.set(17, 0, "h")
        a.shade("h", "H", "u", 2)
    elif side:
        a.ellipse(14.5, 8, 11, 6.5, "h")
        a.rect(5, 8, 11, 17, "h")
        a.rect(19, 10, 25, 10, "h")
        a.rect(13, 1, 14, 2, "h")
        a.shade("h", "H", "u", 1)
        a.set(27, 14, "s"); a.set(27, 15, "s")       # little nose
        eye(a, 22, 13, 4)
        a.rect(20, 18, 21, 18, "m")
        a.set(24, 19, "S")
        a.rect(12, 13, 13, 15, "S")                  # ear
    else:
        a.ellipse(16, 8.5, 11.5, 7, "h")
        a.rect(5, 8, 7, 17, "h"); a.rect(24, 8, 26, 17, "h")
        # bangs
        for x in range(8, 24, 4):
            a.rect(x, 13, x + 1, 13, "h")
        a.rect(15, 0, 16, 1, "h"); a.set(17, 0, "h")
        a.shade("h", "H", "u", 1)
        eye(a, 11, 14, 4); eye(a, 19, 14, 4)
        a.rect(8, 18, 9, 18, "m"); a.rect(22, 18, 23, 18, "m")
        a.rect(15, 19, 16, 19, "S")
    a.shade("t", "T", "v", 1)
    a.outline("k")
    return a


for f in ("down", "up", "side"):
    sprite(f"child_{f}", child(f, False))
    sprite(f"child_{f}_step", child(f, True))


# ---------------------------------------------------------------- Ekqo
# A little light spirit from the Cloud Meadow: a soft, round mochi body, two floppy cloud
# ears with pink insides, a spark on his head, a star on his tummy, stubby feet that hop,
# and big shiny eyes. Expressions: normal, happy, talk, sleepy, tired (dim, before hello).

def ekqo_body(dim=False):
    a = Art(44)
    body, shade = ("U", "V") if dim else ("e", "E")
    # floppy ears, leaning out
    a.poly([(8, 22), (3, 12), (5, 7), (10, 9), (15, 18)], body)
    a.poly([(36, 22), (41, 12), (39, 7), (34, 9), (29, 18)], body)
    # body: round on top, a little wider at the bottom
    a.ellipse(22, 25, 15, 13, body)
    a.ellipse(22, 30, 16.5, 9.5, body)
    # feet
    a.ellipse(14.5, 39.5, 4, 2.5, body)
    a.ellipse(29.5, 39.5, 4, 2.5, body)
    a.shade(body, shade, "F" if not dim else None, 2)
    # inner ears
    a.poly([(6, 11), (8, 10), (11, 16), (8, 17)], "f" if not dim else "M")
    a.poly([(38, 11), (36, 10), (33, 16), (36, 17)], "f" if not dim else "M")
    # spark on his head (a small four-point star)
    spark = "Y" if dim else "y"
    # a little flame of light on his head, its tip leaning to one side
    a.ellipse(22, 9.5, 3.5, 3.5, spark)
    a.poly([(18, 9), (24, 1), (26, 9)], spark)
    a.set(24, 1, spark)
    a.rect(21, 12, 23, 13, spark)
    if not dim:
        a.rect(21, 8, 22, 10, "F")
    # star on his tummy
    t = "E" if not dim else "V"
    a.rect(21, 31, 22, 31, t); a.rect(20, 32, 23, 33, t); a.rect(21, 34, 22, 34, t)
    a.outline("k")
    return a


def ekqo_face(a, mood, dim=False):
    lx, rx, ey = 13, 26, 21
    if mood in ("sleepy", "happy"):
        for x in (lx, rx):
            if mood == "sleepy":       # closed, curved down
                a.rect(x, ey + 3, x + 4, ey + 3, "k"); a.set(x - 1, ey + 2, "k"); a.set(x + 5, ey + 2, "k")
            else:                      # ^ ^ smiling eyes
                a.rect(x + 1, ey + 1, x + 3, ey + 1, "k"); a.set(x, ey + 2, "k"); a.set(x + 4, ey + 2, "k")
                a.set(x - 1, ey + 3, "k"); a.set(x + 5, ey + 3, "k")
    else:
        for x in (lx, rx):
            # big oval eye: dark, with a blue shine at the bottom and two white sparkles
            a.rect(x + 1, ey, x + 3, ey, "k")
            a.rect(x, ey + 1, x + 4, ey + 6, "k")
            a.rect(x + 1, ey + 7, x + 3, ey + 7, "k")
            a.rect(x + 1, ey + 5, x + 3, ey + 6, "B")
            a.rect(x + 1, ey + 1, x + 2, ey + 2, "F")
            a.set(x + 3, ey + 5, "c")
            if dim:                    # tired: heavy lids
                a.rect(x, ey, x + 4, ey + 2, "V")
                a.rect(x, ey + 3, x + 4, ey + 3, "k")
    # cheeks
    a.rect(8, ey + 7, 11, ey + 8, "m" if not dim else "M")
    a.rect(32, ey + 7, 35, ey + 8, "m" if not dim else "M")
    # mouth
    my = ey + 8
    if mood == "talk":
        a.rect(20, my, 23, my + 3, "k")
        a.rect(21, my + 1, 22, my + 2, "C")
        a.rect(21, my + 2, 22, my + 2, "f")
    elif mood == "happy":
        a.rect(19, my, 24, my, "k")
        a.rect(20, my + 1, 23, my + 1, "C")
        a.rect(21, my + 2, 22, my + 2, "k")
        a.set(19, my + 1, "k"); a.set(24, my + 1, "k")
    elif mood == "sleepy" or dim:
        a.rect(21, my + 1, 22, my + 1, "k")
    else:                              # small cat-like smile
        a.set(19, my, "k"); a.set(20, my + 1, "k"); a.set(21, my, "k")
        a.set(22, my, "k"); a.set(23, my + 1, "k"); a.set(24, my, "k")
    return a


def ekqo(mood="normal", dim=False):
    return ekqo_face(ekqo_body(dim), mood, dim)


def ekqo_mud():
    """Mud spots, drawn over Ekqo until his bath (Chapter 1). Same size as Ekqo."""
    a = Art(44)
    for x, y in ((8, 31), (9, 32), (8, 32), (33, 35), (34, 35), (33, 36), (26, 39), (27, 39), (5, 15), (12, 36)):
        a.set(x, y, "O")
    a.set(9, 31, "o"); a.set(34, 34, "o")
    return a


sprite("ekqo_dim", ekqo("normal", dim=True))
sprite("ekqo", ekqo())
sprite("ekqo_happy", ekqo("happy"))
sprite("ekqo_talk", ekqo("talk"))
sprite("ekqo_sleepy", ekqo("sleepy"))
sprite("ekqo_mud", ekqo_mud())


# ---------------------------------------------------------------- word pictures and icons

def card(fill=None):
    return Art(32, fill="." if fill is None else fill)


def pic_star():
    a = card()
    import math
    pts = []
    for i in range(10):
        r = 14 if i % 2 == 0 else 6
        ang = -math.pi / 2 + i * math.pi / 5
        pts.append((round(16 + r * math.cos(ang)), round(17 + r * math.sin(ang))))
    a.poly(pts, "y")
    a.shade("y", "Y", "F", 2)
    a.outline("k")
    return a


def pic_light():
    a = card()
    for i in range(8):
        import math
        ang = i * math.pi / 4
        a.line(round(16 + 11 * math.cos(ang)), round(16 + 11 * math.sin(ang)), round(16 + 14 * math.cos(ang)), round(16 + 14 * math.sin(ang)), "Y")
    a.ellipse(16, 16, 8, 8, "y")
    a.shade("y", "Y", "F", 2)
    a.outline("k")
    return a


def pic_night():
    a = card("B")
    m = Art(32)
    m.ellipse(14, 17, 10, 10, "y")
    m.ellipse(19, 13, 8.5, 8.5, ".")
    m.shade("y", "Y", None, 1)
    m.outline("k")
    a.paste(m, 0, 0)
    for x, y in ((24, 6), (27, 20), (8, 5), (22, 27)):
        a.set(x, y, "F"); a.set(x + 1, y, "c"); a.set(x - 1, y, "c"); a.set(x, y + 1, "c"); a.set(x, y - 1, "c")
    a.rect(0, 0, 31, 0, "k"); a.rect(0, 31, 31, 31, "k"); a.rect(0, 0, 0, 31, "k"); a.rect(31, 0, 31, 31, "k")
    return a


def pic_hello():
    a = card()
    a.rect(10, 12, 21, 26, "s")
    for i, x in enumerate((10, 13, 16, 19)):
        a.rect(x, 4 + (i % 2) * 2, x + 1, 13, "s")
    a.rect(4, 14, 9, 17, "s"); a.rect(6, 12, 9, 14, "s")
    a.shade("s", "S", None, 1)
    a.outline("k")
    for y in (6, 10):
        a.line(25, y, 28, y - 2, "Y")
    a.line(25, 14, 29, 14, "Y")
    return a


def pic_yes():
    a = card()
    a.ellipse(16, 16, 13, 13, "Q")
    a.shade("Q", "G", "l", 2)
    a.outline("k")
    for d in range(3):
        a.line(9, 16 + d, 14, 21 + d, "F")
        a.line(14, 21 + d, 23, 10 + d, "F")
    return a


def pic_no():
    a = card()
    a.ellipse(16, 16, 13, 13, "C")
    a.shade("C", "R", "m", 2)
    a.outline("k")
    for d in range(3):
        a.line(10 + d, 10, 21 + d, 21, "F")
        a.line(21 + d, 10, 10 + d, 21, "F")
    return a


def pic_name():
    a = card()
    a.rect(3, 8, 28, 25, "w")
    a.rect(3, 8, 28, 12, "b")
    a.shade("w", "W", None, 1)
    a.outline("k")
    a.rect(7, 17, 24, 18, "k")
    a.rect(7, 21, 18, 21, "A")
    a.rect(10, 10, 21, 10, "F")
    return a


def pic_garden():
    a = card()
    a.ellipse(16, 24, 14, 6, "d")
    a.shade("d", "D", "L", 2)
    for x, c in ((8, "f"), (16, "y"), (24, "n")):
        a.line(x, 22, x, 12, "D")
        a.set(x + 1, 17, "L"); a.set(x + 2, 16, "L")
        a.ellipse(x + 0.5, 10.5, 3, 3, c)
        a.set(x, 10, "y" if c != "y" else "Y")
    a.outline("k")
    return a


def pic_who():
    a = card()
    q = [
        "..nnnnnn..",
        ".nnnnnnnn.",
        "nnn....nnn",
        "nn......nn",
        ".......nnn",
        "......nnn.",
        ".....nnn..",
        "....nnn...",
        "....nn....",
        "....nn....",
        "..........",
        "....nn....",
        "....nn....",
    ]
    for y, row in enumerate(q):
        for x, ch in enumerate(row):
            if ch != ".":
                for dy in range(2):
                    for dx in range(2):
                        a.set(6 + x * 2 + dx, 3 + y * 2 + dy, ch)
    a.shade("n", "N", None, 1)
    a.outline("k")
    return a


def pic_heart():
    a = card()
    a.ellipse(11, 12, 7, 7, "C")
    a.ellipse(21, 12, 7, 7, "C")
    a.poly([(4, 14), (28, 14), (16, 28)], "C")
    a.shade("C", "R", "m", 2)
    a.set(9, 9, "F"); a.set(10, 9, "F"); a.set(9, 10, "F")
    a.outline("k")
    return a


def pic_house():
    a = card()
    a.rect(7, 15, 24, 28, "w")
    a.shade("w", "W", None, 1)
    a.poly([(3, 16), (16, 4), (29, 16)], "r")
    a.shade("r", "R", "q", 1)
    a.rect(14, 20, 18, 28, "o")
    a.rect(9, 18, 12, 21, "y"); a.rect(20, 18, 23, 21, "y")
    a.outline("k")
    return a


def pic_bed():
    a = card()
    a.rect(3, 12, 6, 28, "o")
    a.rect(26, 18, 28, 28, "o")
    a.rect(5, 19, 27, 24, "b")
    a.rect(5, 17, 12, 19, "F")
    a.rect(12, 17, 27, 19, "b")
    a.shade("b", "B", "c", 1)
    a.outline("k")
    for x, y, s in ((19, 4, 3), (25, 9, 2)):
        a.line(x, y, x + s, y, "n"); a.line(x + s, y, x, y + s, "n"); a.line(x, y + s, x + s, y + s, "n")
    return a


def pic_book():
    a = card()
    a.rect(3, 6, 15, 26, "n"); a.rect(17, 6, 28, 26, "n")
    a.rect(15, 6, 16, 27, "N")
    a.shade("n", "N", None, 1)
    for x in (8, 22):
        a.ellipse(x + 0.5, 16, 3, 3, "y")
    a.outline("k")
    return a


def pic_page():
    a = card()
    a.rect(6, 3, 25, 28, "i")
    a.shade("i", "W", None, 1)
    a.ellipse(15.5, 10, 4, 4, "y")
    for y in (17, 21, 25):
        a.rect(9, y, 22 if y != 25 else 17, y, "A")
    a.outline("k")
    return a


def pic_jar():
    a = card()
    a.rect(8, 9, 23, 28, "c")
    a.ellipse(16, 19, 5.5, 5.5, "y")
    a.set(14, 19, "k"); a.set(18, 19, "k")
    a.rect(7, 4, 24, 8, "o")
    a.rect(7, 4, 24, 4, "j")
    a.set(10, 12, "F"); a.set(10, 13, "F"); a.set(10, 14, "F")
    a.outline("k")
    return a


sprite("pic_hello", pic_hello())
sprite("pic_yes", pic_yes())
sprite("pic_no", pic_no())
sprite("pic_name", pic_name())
sprite("pic_star", pic_star())
sprite("pic_light", pic_light())
sprite("pic_night", pic_night())
sprite("pic_garden", pic_garden())
sprite("pic_who", pic_who())
sprite("pic_dont_be_scared", pic_heart())
sprite("pic_come_to_my_house", pic_house())
sprite("pic_sleep_there", pic_bed())
sprite("book", pic_book())
sprite("page", pic_page())
sprite("jar", pic_jar())


def main():
    lines = [
        "# Ekqo pixel art (original), made by tools/pixelart/generate.py. One palette, then sprites",
        "# drawn with one palette letter per pixel; \".\" is transparent. Tiles are 32x32; other",
        "# sprites can be any size. PixelArt.cs turns each sprite into a texture at start-up.",
        "# Running generate.py again replaces this file. Preview: python3 tools/pixelart/preview.py",
        "",
        "palette",
    ]
    lines += [f"{k} {v}" for k, v, _ in PALETTE]
    lines.append("end")
    for name, art in SPRITES.items():
        lines += ["", f"sprite {name}"] + art.rows() + ["end"]
    with open(OUT, "w", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")
    print(f"wrote {len(SPRITES)} sprites to {os.path.normpath(OUT)}")


if __name__ == "__main__":
    main()
