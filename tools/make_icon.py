# Makes Quiznight?'s icons from the artwork tools/icon-source.png (1254 px, the neon "Q? Quiznight?" picture):
#   assets/icon/icon.ico  the Windows .exe: the whole artwork at 48 px and up (Explorer, desktop); at 16-32 px
#                         (file lists, taskbar) only its Q?, since the word "Quiznight?" can't be read that small
#   assets/icon/icon.png  the window icon (project.godot's config/icon, shown small by the OS): the Q? version
# The artwork's dark corners outside the rounded frame become transparent.
#   py tools/make_icon.py [PREVIEW_DIR]   (PREVIEW_DIR: also saves each size as a PNG to look at)
import sys
from pathlib import Path
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parent.parent
art = Image.open(ROOT / "tools" / "icon-source.png").convert("RGBA")
SIZE = art.width
DARK, BLUE = (7, 1, 23, 255), (0, 201, 246, 255)
# The Q? (with the crown and sparks above it), without the word underneath.
Q_BOX = (165, 150, 1120, 772)

def rounded_mask(size, radius, inset=0):
    # drawn 4x bigger and shrunk, for smooth edges
    big = Image.new("L", (size * 4, size * 4), 0)
    ImageDraw.Draw(big).rounded_rectangle((inset * 4, inset * 4, (size - inset) * 4 - 1, (size - inset) * 4 - 1), radius=radius * 4, fill=255)
    return big.resize((size, size), Image.LANCZOS)

def full(n):
    icon = art.copy()
    icon.putalpha(rounded_mask(SIZE, 300))
    return icon.resize((n, n), Image.LANCZOS)

def small(n):
    # A dark rounded square with the blue edge and the Q? inside it (worked out at 256 px, then shrunk).
    big = 256
    icon = Image.new("RGBA", (big, big), (0, 0, 0, 0))
    ImageDraw.Draw(icon).rounded_rectangle((4, 4, big - 5, big - 5), radius=58, fill=DARK, outline=BLUE, width=14)
    q = art.crop(Q_BOX)
    room = big - 2 * 22
    k = room / max(q.width, q.height)
    q = q.resize((round(q.width * k), round(q.height * k)), Image.LANCZOS)
    layer = Image.new("RGBA", (big, big), (0, 0, 0, 0))
    layer.paste(q, ((big - q.width) // 2, (big - q.height) // 2))
    # (the crop's own background stays inside the blue frame)
    layer.putalpha(Image.composite(layer.getchannel("A"), Image.new("L", (big, big), 0), rounded_mask(big, 48, 16)))
    icon.alpha_composite(layer)
    return icon.resize((n, n), Image.LANCZOS)

def icon(n):
    return small(n) if n <= 32 else full(n)

out = ROOT / "assets" / "icon"
small(256).save(out / "icon.png")
sizes = [16, 24, 32, 48, 64, 128, 256]
frames = [icon(n) for n in sizes]
frames[-1].save(out / "icon.ico", format="ICO", sizes=[(n, n) for n in sizes], append_images=frames[:-1])
if len(sys.argv) > 1:
    for n in sizes:
        icon(n).save(Path(sys.argv[1]) / f"icon-{n}.png")
print("wrote", out / "icon.png", "and", out / "icon.ico")
