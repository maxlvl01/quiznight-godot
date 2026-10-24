# Draws Quiznight?'s icon: "Q?" in the home screen logo's font (Segoe UI Black: the logo asks for Sora, which isn't
# installed, so Godot falls back to it), a white Q and the logo's orange "?", on the purple with a blue edge.
# Writes assets/icon/icon.png (the window icon, project.godot's config/icon) and assets/icon/icon.ico (the Windows
# .exe, each size drawn from a 4x larger picture):
#   py tools/make_icon.py
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

FONT = r"C:\Windows\Fonts\seguibl.ttf"
SCALE = 4
SIZE = 256 * SCALE
PURPLE_TOP, PURPLE_BOTTOM = (0x2E, 0x0F, 0x4A), (0x15, 0x00, 0x23)
BLUE, WHITE, ORANGE = (0x00, 0xC9, 0xF6), (0xEF, 0xEA, 0xF6), (0xFF, 0x82, 0x01)

def s(v):
    return int(v * SCALE)

image = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))

# The rounded square: a purple gradient, cut to shape by a mask, with the blue edge drawn on top.
gradient = Image.new("RGBA", (SIZE, SIZE))
pixels = ImageDraw.Draw(gradient)
for y in range(SIZE):
    t = y / (SIZE - 1)
    pixels.line([(0, y), (SIZE, y)], fill=tuple(round(a + (b - a) * t) for a, b in zip(PURPLE_TOP, PURPLE_BOTTOM)) + (255,))
mask = Image.new("L", (SIZE, SIZE), 0)
box = (s(6), s(6), SIZE - s(6), SIZE - s(6))
ImageDraw.Draw(mask).rounded_rectangle(box, radius=s(54), fill=255)
image.paste(gradient, (0, 0), mask)
draw = ImageDraw.Draw(image)
draw.rounded_rectangle(box, radius=s(54), outline=BLUE, width=s(8))

# "Q?" as one word, centred on its ink (not on the font's line box), like the logo's last letter and "?".
font = ImageFont.truetype(FONT, s(190))
q_box = draw.textbbox((0, 0), "Q", font=font)
mark_box = draw.textbbox((0, 0), "?", font=font)
q_advance = draw.textlength("Q", font=font)
left = min(q_box[0], q_advance + mark_box[0])
right = max(q_box[2], q_advance + mark_box[2])
top = min(q_box[1], mark_box[1])
bottom = max(q_box[3], mark_box[3])
x = (SIZE - (right - left)) / 2 - left
y = (SIZE - (bottom - top)) / 2 - top
draw.text((x, y), "Q", font=font, fill=WHITE)
draw.text((x + q_advance, y), "?", font=font, fill=ORANGE)

out = Path(__file__).resolve().parent.parent / "assets" / "icon"
out.mkdir(exist_ok=True)
image.resize((256, 256), Image.LANCZOS).save(out / "icon.png")
sizes = [16, 24, 32, 48, 64, 128, 256]
frames = [image.resize((n, n), Image.LANCZOS) for n in sizes]
frames[-1].save(out / "icon.ico", format="ICO", sizes=[(n, n) for n in sizes], append_images=frames[:-1])
print("wrote", out / "icon.png", "and", out / "icon.ico")
