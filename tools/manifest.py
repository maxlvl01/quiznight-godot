"""Makes a build's update manifest for the in-game updater (see scripts/Storage/Updater.cs).

    py -I tools/manifest.py VERSION BUILD_DIR OUT_JSON BLOB_DIR BASE_URL [PREVIOUS_MANIFEST ...]

Lists every file of BUILD_DIR with its size and SHA-256. A file whose hash an earlier manifest already has
keeps that manifest's download URL (it's still in that older release); any other file is gzipped into
BLOB_DIR as "<sha256>.gz", to be uploaded to this release (BASE_URL + name). So a release only uploads, and
a game only downloads, the files that changed. Never delete old releases: newer manifests point into them.
"""
import gzip, hashlib, json, os, shutil, sys

version, build, out_json, blob_dir, base_url, *previous = sys.argv[1:]
known = {}
for p in previous:
    if os.path.isfile(p):
        with open(p, encoding="utf-8") as f:
            for entry in json.load(f)["files"]:
                known[entry["sha256"]] = (entry["url"], entry["gz"])

os.makedirs(blob_dir, exist_ok=True)
files, new_bytes = [], 0
for root, _, names in os.walk(build):
    for name in sorted(names):
        full = os.path.join(root, name)
        rel = os.path.relpath(full, build).replace(os.sep, "/")
        h = hashlib.sha256()
        with open(full, "rb") as f:
            for chunk in iter(lambda: f.read(1 << 20), b""):
                h.update(chunk)
        sha = h.hexdigest()
        if sha in known:
            url, gz = known[sha]
        else:
            blob = os.path.join(blob_dir, sha + ".gz")
            if not os.path.exists(blob):  # (the other platform may share the file, e.g. the maps)
                with open(full, "rb") as src, gzip.open(blob, "wb", compresslevel=9) as dst:
                    shutil.copyfileobj(src, dst, 1 << 20)
                new_bytes += os.path.getsize(blob)
            url, gz = base_url + sha + ".gz", os.path.getsize(blob)
            known[sha] = (url, gz)
        files.append({"path": rel, "size": os.path.getsize(full), "sha256": sha, "url": url, "gz": gz})

files.sort(key=lambda e: e["path"])
with open(out_json, "w", encoding="utf-8") as f:
    json.dump({"version": version, "files": files}, f, indent=0)
print(f"{out_json}: {len(files)} files, {new_bytes / 1e6:.1f} MB of new blobs")
