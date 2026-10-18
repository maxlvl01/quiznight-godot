#!/usr/bin/env bash
# Builds the game and publishes it as a GitHub release that the in-game updater picks up.
#   tools/release.sh            build + self-test + zip + publish the version in project.godot
#   tools/release.sh --dry-run  everything except publishing
# The release notes are this version's section of CHANGELOG.md. The release goes to the public
# builds-only repo (the code repo, maxlvl01/quiznight-godot, stays private). Needs `gh` logged in and the export templates.
set -euo pipefail
cd "$(dirname "$0")/.."

G="${GODOT:-/c/Users/Xbox3/Desktop/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe}"
REPO="maxlvl01/quiznight-releases"
VERSION=$(sed -n 's/^config\/version="\(.*\)"/\1/p' project.godot)
OUT="build/Quiznight"
ZIP="build/Quiznight-$VERSION-windows.zip"
NOTES="build/notes-$VERSION.md"
DRY=${1:-}

echo "== Quiznight $VERSION"
grep -q "^## \[$VERSION\]" CHANGELOG.md || { echo "CHANGELOG.md has no section '## [$VERSION]'"; exit 1; }

dotnet build "QuizNight!.csproj" -nologo -v q
rm -rf "$OUT" && mkdir -p "$OUT"
"$G" --headless --path . --export-release "Windows Desktop" "$OUT/Quiznight.exe" > build/export.log 2>&1 || true
[ -f "$OUT/Quiznight.exe" ] && [ -d "$OUT/data_QuizNight!_windows_x86_64" ] || { echo "Export failed, see build/export.log"; exit 1; }

echo "== Self-test of the exported build"
"$OUT/Quiznight.exe" --headless -- --selftest > build/selftest.log 2>&1 || { tail -20 build/selftest.log; exit 1; }
tail -1 build/selftest.log

echo "== Zipping"
rm -f "$ZIP"
powershell -NoProfile -Command "Compress-Archive -Path '$OUT/*' -DestinationPath '$ZIP' -CompressionLevel Optimal"
ls -la "$ZIP"

# This version's notes: from its "## [version]" line up to the next "## [".
awk -v v="## [$VERSION]" 'index($0, v) == 1 { on = 1; next } /^## \[/ { on = 0 } on' CHANGELOG.md > "$NOTES"

if [ "$DRY" = "--dry-run" ]; then
	echo "== Dry run: not publishing ($ZIP, notes in $NOTES)"
	exit 0
fi

echo "== Publishing v$VERSION to $REPO"
gh release create "v$VERSION" "$ZIP" --repo "$REPO" --title "Quiznight $VERSION" --notes-file "$NOTES" --latest
echo "Done: https://github.com/$REPO/releases/tag/v$VERSION"
