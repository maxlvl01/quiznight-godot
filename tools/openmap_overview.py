# Builds the WoW map's in-between zoom steps from OpenMap's stitched zone tiles (assets/maps/wow/stitched): the
# tiles put together into one big map, saved downsized (assets/maps/wow/overview-<n>.webp, n = pixels per world-map
# pixel). Between the world view and the zone tiles, the map draws whichever of these fits the zoom, so every step
# shows the same art, only sharper. Transparent where no tile is (the sea: the map draws its own sea underneath).
# Run: py -I tools/openmap_overview.py   (then reimport: Godot --headless --editor --path . --quit)
import os
import sys
from PIL import Image

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
TILES = os.path.join(ROOT, "assets", "maps", "wow", "stitched")
OUT = os.path.join(ROOT, "assets", "maps", "wow")
# OpenMap's bake (WowMapArt): 16 px per world-map pixel, 512-px tiles whose middle 510 px are their own.
TILE_PX, STEP = 16, 510
WORLD_W, WORLD_H = 1002, 668
SIZES = [2, 4]

def main():
    full_w, full_h = WORLD_W * TILE_PX, WORLD_H * TILE_PX
    tiles = []
    for name in os.listdir(TILES):
        if not name.endswith(".webp"):
            continue
        i, j = (int(n) for n in name[:-5].split("_"))
        tiles.append((i, j, os.path.join(TILES, name)))
    if not tiles:
        sys.exit("no tiles in " + TILES)
    for px in SIZES:
        scale = px / TILE_PX
        canvas = Image.new("RGBA", (round(full_w * scale), round(full_h * scale)), (0, 0, 0, 0))
        step = STEP * scale
        for i, j, path in tiles:
            with Image.open(path) as tile:
                own = tile.convert("RGBA").crop((1, 1, 1 + STEP, 1 + STEP))
                # (a pixel larger, so neighbours overlap instead of leaving a hairline)
                size = int(step) + 2
                canvas.alpha_composite(own.resize((size, size), Image.LANCZOS), (int(i * step), int(j * step)))
        path = os.path.join(OUT, f"overview-{px}.webp")
        canvas.save(path, "WEBP", quality=82, method=6)
        print(f"{path}: {canvas.width}x{canvas.height}, {os.path.getsize(path) // 1024} KB")

if __name__ == "__main__":
    main()
