#!/usr/bin/env bash
# Bumps the last number of config/version (X.Y.Z or X.Y.Z.W) in project.godot, writes it to the Windows export preset too, and prints it.
# If the version files already differ from HEAD (an interrupted earlier run), prints that version unchanged.
# The macOS preset leaves its version fields empty, so the bundle takes config/version.
set -eu
ROOT="$(cd "$(dirname "$0")/../../../.." && pwd)"
PRESET="$ROOT/export_presets.cfg"
PROJECT="$ROOT/project.godot"

current="$(sed -nE 's/^config\/version="([0-9]+(\.[0-9]+){2,3})"$/\1/p' "$PROJECT")"
[ -n "$current" ] || { echo "No X.Y.Z or X.Y.Z.W config/version in $PROJECT" >&2; exit 1; }

if ! git -C "$ROOT" diff --quiet HEAD -- export_presets.cfg project.godot; then echo "$current"; exit 0; fi

version="${current%.*}.$(( ${current##*.} + 1 ))"
file_version="$version"; [ "$(tr -cd . <<< "$version")" = ".." ] && file_version="$version.0"
sed -i '' -E "s/^config\/version=\"[^\"]*\"$/config\/version=\"$version\"/" "$PROJECT"
sed -i '' -E "s/^application\/(file|product)_version=\"[^\"]*\"$/application\/\1_version=\"$file_version\"/" "$PRESET"
echo "$version"
