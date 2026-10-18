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

dotnet build "QuizNight!.csproj" -nologo -v q
rm -rf "$OUT" && mkdir -p "$OUT"
"$G" --headless --path . --export-release "Windows Desktop" "$OUT/Quiznight.exe" > build/export.log 2>&1 || true
[ -f "$OUT/Quiznight.exe" ] && [ -d "$OUT/data_QuizNight!_windows_x86_64" ] || { echo "Export failed, see build/export.log"; exit 1; }
# AnyConverter (our MP4 → .ogv converter, a separate project) ships with the Windows build; the video guide opens it.
# FFmpeg isn't bundled: AnyConverter helps players install it the first time.
[ -f "$ANYCONVERTER" ] || { echo "AnyConverter.exe not found at $ANYCONVERTER (build it with PyInstaller, or set ANYCONVERTER)"; exit 1; }
mkdir -p "$OUT/AnyConverter" && cp "$ANYCONVERTER" "$OUT/AnyConverter/AnyConverter.exe"

echo "== Linux build"
rm -rf "$OUT_LINUX" && mkdir -p "$OUT_LINUX"
"$G" --headless --path . --export-release "Linux" "$OUT_LINUX/Quiznight.x86_64" > build/export-linux.log 2>&1 || true
[ -f "$OUT_LINUX/Quiznight.x86_64" ] && [ -f "$OUT_LINUX/data_QuizNight!_linuxbsd_x86_64/libsteam_api.so" ] || { echo "Linux export failed, see build/export-linux.log"; exit 1; }
# AnyConverter's native Linux build (from its GitHub Actions run or build_linux.sh, packed as AnyConverter-linux/AnyConverter).
[ -f "$ANYCONVERTER_LINUX" ] || { echo "AnyConverter for Linux not found at $ANYCONVERTER_LINUX (set ANYCONVERTER_LINUX)"; exit 1; }
rm -rf build/anyconverter-linux && mkdir -p build/anyconverter-linux "$OUT_LINUX/AnyConverter"
/c/Windows/System32/tar.exe -x -f "$ANYCONVERTER_LINUX" -C build/anyconverter-linux
cp build/anyconverter-linux/AnyConverter-linux/AnyConverter "$OUT_LINUX/AnyConverter/AnyConverter"
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

# This version's notes: from its "## [version]" line up to the next "## [".
awk -v v="## [$VERSION]" 'index($0, v) == 1 { on = 1; next } /^## \[/ { on = 0 } on' CHANGELOG.md > "$NOTES"

if [ "$DRY" = "--dry-run" ]; then
	echo "== Dry run: not publishing ($ZIP, notes in $NOTES)"
	exit 0
fi

echo "== Publishing v$VERSION to $REPO"
if gh release view "v$VERSION" --repo "$REPO" >/dev/null 2>&1; then
	# Same version again (e.g. a rebuilt build): replace the files and notes.
	gh release upload "v$VERSION" "$ZIP" "$ZIP_LINUX" --repo "$REPO" --clobber
	gh release edit "v$VERSION" --repo "$REPO" --notes-file "$NOTES"
else
	gh release create "v$VERSION" "$ZIP" "$ZIP_LINUX" --repo "$REPO" --title "Quiznight $VERSION" --notes-file "$NOTES" --latest
fi
echo "Done: https://github.com/$REPO/releases/tag/v$VERSION"
