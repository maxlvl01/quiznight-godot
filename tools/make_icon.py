# Packs the PNGs from tools/render_icon.gd into assets/icon/icon.ico (the Windows .exe icon), each size drawn
# sharp at its own resolution instead of shrunk from the biggest:
#   py tools/make_icon.py PNG_DIR
import sys
from pathlib import Path
from PIL import Image

src = Path(sys.argv[1])
sizes = [16, 24, 32, 48, 64, 128, 256]
images = [Image.open(src / f"icon-{s}.png").convert("RGBA") for s in sizes]
out = Path(__file__).resolve().parent.parent / "assets" / "icon" / "icon.ico"
out.parent.mkdir(exist_ok=True)
images[-1].save(out, format="ICO", sizes=[(s, s) for s in sizes], append_images=images[:-1])
print(out, out.stat().st_size, "bytes")
