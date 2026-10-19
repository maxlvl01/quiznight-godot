"""Makes a build's update list for the in-game updater (see scripts/Storage/Updater.cs).

    py -I tools/manifest.py VERSION PLATFORM BUILD_DIR OUT_JSON PACK_DIR BASE_URL [PREVIOUS_LIST ...]

Lists every file of BUILD_DIR with its size and SHA-256, and puts the files in a few packs (zips) by kind:
"game" (the .pck and the game's own DLL: changes every release), "engine" (Godot and .NET: changes with a new
Godot version), "maps" (the built-in maps, the same for both systems) and "anyconverter". A pack's name carries
a hash of what's in it; when an earlier list has the same pack, its download URL is kept (it's still in that
older release), else the pack is zipped into PACK_DIR for this release (BASE_URL + name). So a release usually
uploads only the two game packs, and a game downloads only the packs with files it doesn't have.
Never delete old releases: newer lists point into them.
"""
import hashlib, json, os, sys, zipfile

version, platform, build, out_json, pack_dir, base_url, *previous = sys.argv[1:]
known = {}
for p in previous:
    if os.path.isfile(p):
        with open(p, encoding="utf-8") as f:
            for name, pack in json.load(f)["packs"].items():
                known[name] = pack


def kind(path):
    if path.startswith("maps/"):
        return "maps"
    if path.startswith("AnyConverter/"):
        return "anyconverter"
    if path.endswith(".pck") or os.path.basename(path).startswith("QuizNight!"):
        return "game"
    return "engine"


files = []
for root, _, names in os.walk(build):
    for name in names:
        full = os.path.join(root, name)
        rel = os.path.relpath(full, build).replace(os.sep, "/")
        h = hashlib.sha256()
        with open(full, "rb") as f:
            for chunk in iter(lambda: f.read(1 << 20), b""):
                h.update(chunk)
        files.append({"path": rel, "size": os.path.getsize(full), "sha256": h.hexdigest(), "kind": kind(rel)})
files.sort(key=lambda e: e["path"])

os.makedirs(pack_dir, exist_ok=True)
packs, new_bytes = {}, 0
for k in sorted({e["kind"] for e in files}):
    members = [e for e in files if e["kind"] == k]
    content = hashlib.sha256("".join(f"{e['path']}:{e['sha256']}\n" for e in members).encode()).hexdigest()[:12]
    # (the maps are the same on both systems: one pack for both)
    name = f"update-{k}-{content}.zip" if k == "maps" else f"update-{k}-{platform}-{content}.zip"
    if name in known:
        packs[name] = known[name]
    else:
        path = os.path.join(pack_dir, name)
        if not os.path.exists(path):
            # (maps and other compressed files are stored as they are)
            with zipfile.ZipFile(path, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as z:
                for e in members:
                    stored = e["path"].endswith((".webp", ".png", ".ogv", ".pck"))
                    z.write(os.path.join(build, e["path"]), e["path"], zipfile.ZIP_STORED if stored else zipfile.ZIP_DEFLATED)
            new_bytes += os.path.getsize(path)
        h = hashlib.sha256()
        with open(path, "rb") as f:
            for chunk in iter(lambda: f.read(1 << 20), b""):
                h.update(chunk)
        packs[name] = {"url": base_url + name, "size": os.path.getsize(path), "sha256": h.hexdigest()}
    for e in members:
        e["pack"] = name
for e in files:
    del e["kind"]

with open(out_json, "w", encoding="utf-8") as f:
    json.dump({"version": version, "files": files, "packs": packs}, f, indent=0)
print(f"{out_json}: {len(files)} files in {len(packs)} packs, {new_bytes / 1e6:.1f} MB of new packs")
