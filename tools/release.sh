#!/usr/bin/env bash
# Builds the game and publishes it as a GitHub release that the in-game updater picks up.
#   tools/release.sh            build (Windows + Linux) + self-test + zip + publish the version in project.godot
#   tools/release.sh --dry-run  everything except publishing
# The release notes are this version's section of CHANGELOG.md. The release goes to the public
# builds-only repo (the code repo, maxlvl01/quiznight-godot, stays private). Needs `gh` logged in and the export templates.
set -euo pipefail
cd "$(dirname "$0")/.."

G="${GODOT:-/c/Users/Xbox3/Desktop/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe}"
REPO="maxlvl01/quiznight-releases"
ANYCONVERTER="${ANYCONVERTER:-/c/Users/Xbox3/Desktop/AnyConverter/dist/AnyConverter.exe}"
ANYCONVERTER_LINUX="${ANYCONVERTER_LINUX:-/c/Users/Xbox3/Desktop/AnyConverter/dist/linux/AnyConverter-linux-x86_64.tar.gz}"
VERSION=$(sed -n 's/^config\/version="\(.*\)"/\1/p' project.godot)
OUT="build/Quiznight"
ZIP="build/Quiznight-$VERSION-windows.zip"
OUT_LINUX="build/Quiznight-linux"
ZIP_LINUX="build/Quiznight-$VERSION-linux.zip"
NOTES="build/notes-$VERSION.md"
DRY=${1:-}

echo "== Quiznight $VERSION"
grep -q "^## \[$VERSION\]" CHANGELOG.md || { echo "CHANGELOG.md has no section '## [$VERSION]'"; exit 1; }
# The token that lets games upload their results to maxlvl01/quiznight-results (Storage/ResultsUpload.cs); not in git.
[ -s results-token.txt ] || echo "Warning: results-token.txt is missing: this build won't upload quiz night results."

# (keeps Godot from importing what's in build/, e.g. the maps copied next to the Windows build)
mkdir -p build && touch build/.gdignore
dotnet build "QuizNight!.csproj" -nologo -v q
rm -rf "$OUT" && mkdir -p "$OUT"
"$G" --headless --path . --export-release "Windows Desktop" "$OUT/Quiznight.exe" > build/export.log 2>&1 || true
[ -f "$OUT/Quiznight.exe" ] && [ -d "$OUT/data_QuizNight!_windows_x86_64" ] || { echo "Export failed, see build/export.log"; exit 1; }
# AnyConverter (our MP4 → .ogv converter, a separate project) ships with the Windows build; the video guide opens it.
# FFmpeg isn't bundled: AnyConverter helps players install it the first time.
[ -f "$ANYCONVERTER" ] || { echo "AnyConverter.exe not found at $ANYCONVERTER (build it with PyInstaller, or set ANYCONVERTER)"; exit 1; }
mkdir -p "$OUT/AnyConverter" && cp "$ANYCONVERTER" "$OUT/AnyConverter/AnyConverter.exe"
# The built-in maps aren't in the .pck (export_presets excludes them): they go next to the game as plain
# files, so updates only download the maps that changed (Images.BuiltinFile).
copy_maps() { mkdir -p "$1/maps" && (cd assets/maps && find . -name '*.webp' -exec cp --parents {} "../../$1/maps/" \;); }
copy_maps "$OUT"
# .NET's debugger and crash-dump helpers and its spare garbage collector come with every export; players never use them
# (crash reports are our own, Storage/CrashReports). ~6.5 MB.
strip_dotnet() { (cd "$1" && rm -f mscordbi.dll mscordaccore.dll mscordaccore_amd64_amd64_*.dll Microsoft.DiaSymReader.Native.amd64.dll createdump.exe clrgc.dll   libmscordbi.so libmscordaccore.so createdump libclrgc.so); }
strip_dotnet "$OUT/data_QuizNight!_windows_x86_64"

echo "== Linux build"
rm -rf "$OUT_LINUX" && mkdir -p "$OUT_LINUX"
"$G" --headless --path . --export-release "Linux" "$OUT_LINUX/Quiznight.x86_64" > build/export-linux.log 2>&1 || true
[ -f "$OUT_LINUX/Quiznight.x86_64" ] && [ -f "$OUT_LINUX/data_QuizNight!_linuxbsd_x86_64/libsteam_api.so" ] || { echo "Linux export failed, see build/export-linux.log"; exit 1; }
# AnyConverter's native Linux build (from its GitHub Actions run or build_linux.sh, packed as AnyConverter-linux/AnyConverter).
[ -f "$ANYCONVERTER_LINUX" ] || { echo "AnyConverter for Linux not found at $ANYCONVERTER_LINUX (set ANYCONVERTER_LINUX)"; exit 1; }
rm -rf build/anyconverter-linux && mkdir -p build/anyconverter-linux "$OUT_LINUX/AnyConverter"
/c/Windows/System32/tar.exe -x -f "$ANYCONVERTER_LINUX" -C build/anyconverter-linux
cp build/anyconverter-linux/AnyConverter-linux/AnyConverter "$OUT_LINUX/AnyConverter/AnyConverter"
copy_maps "$OUT_LINUX"
strip_dotnet "$OUT_LINUX/data_QuizNight!_linuxbsd_x86_64"
# (the export builds the C# for Linux; build it for Windows again for the editor and the self-test)
dotnet build "QuizNight!.csproj" -nologo -v q

echo "== Self-test of the exported build"
"$OUT/Quiznight.exe" --headless -- --selftest > build/selftest.log 2>&1 || { tail -20 build/selftest.log; exit 1; }
tail -1 build/selftest.log

echo "== Zipping"
rm -f "$ZIP"
powershell -NoProfile -Command "Compress-Archive -Path '$OUT/*' -DestinationPath '$ZIP' -CompressionLevel Optimal"
ls -la "$ZIP"
# Windows' tar writes zips with forward slashes, which Linux needs.
rm -f "$ZIP_LINUX"
(cd "$OUT_LINUX" && /c/Windows/System32/tar.exe -a -c -f "../../$ZIP_LINUX" *)
ls -la "$ZIP_LINUX"

echo "== Update lists"
# Every file with its hash, in a few packs by kind (game, engine, maps, AnyConverter). Packs that changed since
# the last release are zipped into $PACKS for upload (usually just the game packs); the rest point to the older
# release that has them (tools/manifest.py). The game downloads only the packs it needs.
PACKS="build/packs-$VERSION"; PREV="build/prev-lists"
rm -rf "$PACKS" "$PREV" && mkdir -p "$PREV"
gh release download --repo "$REPO" --pattern 'update-*.json' -D "$PREV" 2>/dev/null || echo "(no earlier update lists: every pack is new)"
BASE="https://github.com/$REPO/releases/download/v$VERSION/"
py -I tools/manifest.py "$VERSION" windows "$OUT" build/update-windows.json "$PACKS" "$BASE" "$PREV"/update-*.json
py -I tools/manifest.py "$VERSION" linux "$OUT_LINUX" build/update-linux.json "$PACKS" "$BASE" "$PREV"/update-*.json build/update-windows.json

# Big updates (x.y.0) get a name: '## [0.8.0b] — "Jokers Wild": …' makes the release "Quiznight 0.8.0b — Jokers Wild".
NAME=$(sed -n "s/^## \[$VERSION\] — \"\([^\"]*\)\".*/\1/p" CHANGELOG.md)
TITLE="Quiznight? $VERSION${NAME:+ — $NAME}"
# This version's notes: from its "## [version]" line up to the next "## [".
awk -v v="## [$VERSION]" 'index($0, v) == 1 { on = 1; next } /^## \[/ { on = 0 } on' CHANGELOG.md > "$NOTES"

if [ "$DRY" = "--dry-run" ]; then
	echo "== Dry run: not publishing ($ZIP, notes in $NOTES)"
	exit 0
fi

echo "== Publishing v$VERSION to $REPO"
# As a draft first, so no game sees the release before all its files are there; the update lists go up last.
if gh release view "v$VERSION" --repo "$REPO" >/dev/null 2>&1; then
	# Same version again (e.g. a rebuilt build): replace the files and notes.
	gh release edit "v$VERSION" --repo "$REPO" --title "$TITLE" --notes-file "$NOTES"
else
	gh release create "v$VERSION" --repo "$REPO" --title "$TITLE" --notes-file "$NOTES" --draft
fi
gh release upload "v$VERSION" "$ZIP" "$ZIP_LINUX" --repo "$REPO" --clobber
find "$PACKS" -name '*.zip' -print0 | xargs -0 -r gh release upload "v$VERSION" --repo "$REPO" --clobber
gh release upload "v$VERSION" build/update-windows.json build/update-linux.json --repo "$REPO" --clobber
gh release edit "v$VERSION" --repo "$REPO" --draft=false --latest
echo "Done: https://github.com/$REPO/releases/tag/v$VERSION"
