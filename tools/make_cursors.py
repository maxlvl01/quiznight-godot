# Makes Quiznight?'s mouse cursors (assets/cursors/*.png) in the game's party show style: chunky white shapes with a thick
# dark ink outline and a small drop shadow underneath, like the buttons' 3D edge. Each is drawn at 64 units, 4x bigger
# for smooth edges, and saved at SIZE px. App sets them with Input.SetCustomMouseCursor; HOTSPOTS (in 64 units) says
# where each one points.
#   py tools/make_cursors.py [PREVIEW]   (PREVIEW: also saves a sheet of all of them, big, to look at)
import sys
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter, ImageChops

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "assets" / "cursors"
SIZE = 40
S = 4  # drawing scale: 64 units -> 256 px
W = 64 * S
INK = (13, 0, 22, 255)
WHITE = (255, 255, 255, 255)
ACCENT = (255, 130, 1, 255)
CORAL = (255, 90, 95, 255)
OUTLINE = 4  # units
SHADOW = 3   # units down

def P(pts):
    return [(x * S, y * S) for x, y in pts]

def canvas():
    return Image.new("L", (W, W), 0)

def finish(fill_mask, details=None, fill=WHITE):
    """The shape's mask -> shadow, ink outline, fill, then details (ink lines etc. drawn by details(draw))."""
    grow = OUTLINE * S * 2 + 1
    outline = fill_mask.filter(ImageFilter.MaxFilter(grow if grow % 2 else grow + 1))
    img = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    shadow = ImageChops.offset(outline, 0, SHADOW * S)
    img.paste((13, 0, 22, 110), (0, 0), shadow)
    img.paste(INK, (0, 0), outline)
    img.paste(fill, (0, 0), fill_mask)
    if details:
        details(ImageDraw.Draw(img))
    return img.resize((SIZE, SIZE), Image.LANCZOS)

def arrow():
    m = canvas(); d = ImageDraw.Draw(m)
    d.polygon(P([(8, 6), (8, 46), (18, 37), (25, 53), (33, 49), (26, 34), (39, 33)]), fill=255)
    def det(dr):
        dr.polygon(P([(11, 13), (11, 22), (17, 17)]), fill=ACCENT)
    return finish(m, det)

def hand():
    m = canvas(); d = ImageDraw.Draw(m)
    r = 5 * S
    d.rounded_rectangle(P([(22, 6), (31, 36)]), r, fill=255)          # index finger
    d.rounded_rectangle(P([(30, 22), (38, 38)]), r, fill=255)          # middle
    d.rounded_rectangle(P([(37, 25), (45, 40)]), r, fill=255)          # ring
    d.rounded_rectangle(P([(44, 29), (51, 43)]), r - S, fill=255)      # little
    d.rounded_rectangle(P([(16, 30), (51, 56)]), 9 * S, fill=255)      # palm
    d.polygon(P([(10, 33), (16, 30), (24, 42), (18, 47)]), fill=255)   # thumb
    d.ellipse(P([(8, 30), (16, 38)]), fill=255)
    def det(dr):
        for x in (31, 38, 45):
            dr.line(P([(x - 0.5, 36), (x - 0.5, 42)]), fill=INK, width=int(2.2 * S))
    return finish(m, det)

def fist():
    m = canvas(); d = ImageDraw.Draw(m)
    r = 5 * S
    for i, x in enumerate((17, 25, 33, 41)):
        d.rounded_rectangle(P([(x, 18), (x + 9, 34)]), r, fill=255)
    d.rounded_rectangle(P([(14, 26), (51, 54)]), 9 * S, fill=255)
    d.polygon(P([(9, 30), (16, 27), (22, 40), (16, 45)]), fill=255)
    d.ellipse(P([(7, 27), (15, 35)]), fill=255)
    def det(dr):
        for x in (25.5, 33.5, 41.5):
            dr.line(P([(x, 20), (x, 30)]), fill=INK, width=int(2.2 * S))
    return finish(m, det)

def ibeam():
    m = canvas(); d = ImageDraw.Draw(m)
    d.rounded_rectangle(P([(29, 12), (35, 52)]), 2 * S, fill=255)
    d.rounded_rectangle(P([(22, 9), (42, 15)]), 3 * S, fill=255)
    d.rounded_rectangle(P([(22, 49), (42, 55)]), 3 * S, fill=255)
    return finish(m)

def forbidden():
    m = canvas(); d = ImageDraw.Draw(m)
    d.ellipse(P([(12, 12), (52, 52)]), fill=255)
    hole = canvas(); hd = ImageDraw.Draw(hole)
    hd.ellipse(P([(19, 19), (45, 45)]), fill=255)
    hd.polygon(P([(22, 18), (18, 22), (42, 46), (46, 42)]), fill=0)
    m = ImageChops.subtract(m, hole)
    return finish(m, fill=CORAL)

def busy():
    m = canvas(); d = ImageDraw.Draw(m)
    d.rounded_rectangle(P([(16, 8), (48, 14)]), 3 * S, fill=255)
    d.rounded_rectangle(P([(16, 50), (48, 56)]), 3 * S, fill=255)
    d.polygon(P([(19, 13), (45, 13), (34, 32), (45, 51), (19, 51), (30, 32)]), fill=255)
    def det(dr):
        dr.polygon(P([(23, 47), (41, 47), (32, 38)]), fill=ACCENT)
        dr.polygon(P([(25, 18), (39, 18), (32, 26)]), fill=ACCENT)
    return finish(m, det)

def cross():
    m = canvas(); d = ImageDraw.Draw(m)
    d.rounded_rectangle(P([(29, 6), (35, 58)]), 2 * S, fill=255)
    d.rounded_rectangle(P([(6, 29), (58, 35)]), 2 * S, fill=255)
    def det(dr):
        dr.ellipse(P([(28.5, 28.5), (35.5, 35.5)]), fill=ACCENT)
    return finish(m, det)

def double_arrow(vertical):
    m = canvas(); d = ImageDraw.Draw(m)
    pts = [(10, 32), (22, 19), (22, 27), (42, 27), (42, 19), (54, 32), (42, 45), (42, 37), (22, 37), (22, 45)]
    if vertical:
        pts = [(y, x) for x, y in pts]
    d.polygon(P(pts), fill=255)
    return finish(m)

def move():
    m = canvas(); d = ImageDraw.Draw(m)
    d.polygon(P([(10, 32), (19, 23), (19, 29), (29, 29), (29, 19), (23, 19), (32, 10), (41, 19), (35, 19), (35, 29), (45, 29), (45, 23),
                 (54, 32), (45, 41), (45, 35), (35, 35), (35, 45), (41, 45), (32, 54), (23, 45), (29, 45), (29, 35), (19, 35), (19, 41)]), fill=255)
    return finish(m)

CURSORS = {
    "arrow": (arrow, (8, 6)),
    "hand": (hand, (26, 7)),
    "drag": (fist, (32, 34)),
    "ibeam": (ibeam, (32, 32)),
    "forbidden": (forbidden, (32, 32)),
    "busy": (busy, (32, 32)),
    "cross": (cross, (32, 32)),
    "hsize": (lambda: double_arrow(False), (32, 32)),
    "vsize": (lambda: double_arrow(True), (32, 32)),
    "move": (move, (32, 32)),
}

if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    made = {}
    for name, (draw, hot) in CURSORS.items():
        img = draw()
        img.save(OUT / f"{name}.png")
        made[name] = img
        print(name, "hotspot", tuple(round(v * SIZE / 64) for v in hot))
    if len(sys.argv) > 1:
        sheet = Image.new("RGBA", (len(made) * SIZE * 3, SIZE * 3), (58, 18, 96, 255))
        for i, img in enumerate(made.values()):
            sheet.paste(img.resize((SIZE * 3, SIZE * 3), Image.NEAREST), (i * SIZE * 3, 0), img.resize((SIZE * 3, SIZE * 3), Image.NEAREST))
        sheet.save(sys.argv[1])
