#!/usr/bin/env bash
# Release-exports the "Windows Desktop" and "macOS" presets into emptied Builds/windows and Builds/macOS,
# and checks both are shippable. Usage: export.sh X.Y.Z   (the version the macOS bundle must carry)
set -u
VERSION="${1:?usage: export.sh X.Y.Z}"
ROOT="$(cd "$(dirname "$0")/../../../.." && pwd)"
GODOT="${GODOT:-/Applications/Godot_mono.app/Contents/MacOS/Godot}"
WIN="$ROOT/Builds/windows"
MAC="$ROOT/Builds/macOS"
APP="$MAC/Quartermaster General.app"
LOG="${TMPDIR:-/tmp}/qg-release-export"

rm -rf "$WIN" "$MAC" && mkdir -p "$WIN" "$MAC"

"$GODOT" --headless --path "$ROOT" --export-release "Windows Desktop" "$WIN/Quartermaster General.exe" > "$LOG-windows.log" 2>&1
win_exit=$?
"$GODOT" --headless --path "$ROOT" --export-release "macOS" "$APP" > "$LOG-macos.log" 2>&1
mac_exit=$?

# Headless runs can delete tracked GodotSteam binaries (see headless-shader-check memory); put them back.
git -C "$ROOT" checkout -- addons/godotsteam/
git -C "$ROOT" clean -fq -- addons/godotsteam/

missing=()
for f in "Quartermaster General.exe" "Quartermaster General.pck" "data_Quartermaster General_windows_x86_64" \
         "libgodotsteam.windows.template_release.x86_64.dll" "steam_api64.dll"; do
    [ -e "$WIN/$f" ] || missing+=("windows/$f")
done
for f in "Contents/MacOS/Quartermaster General WW2" \
         "Contents/Resources/Quartermaster General WW2.pck" \
         "Contents/Resources/data_Quartermaster General_macos_arm64" \
         "Contents/Resources/data_Quartermaster General_macos_x86_64" \
         "Contents/Frameworks/libgodotsteam.macos.template_release.dylib" \
         "Contents/Frameworks/libsteam_api.dylib"; do
    [ -e "$APP/$f" ] || missing+=("macOS/$f")
done
debug=()
[ -e "$WIN/libgodotsteam.windows.template_debug.x86_64.dll" ] && debug+=(windows)
[ -e "$APP/Contents/Frameworks/libgodotsteam.macos.template_debug.dylib" ] && debug+=(macOS)
plist_version="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleShortVersionString' "$APP/Contents/Info.plist" 2>/dev/null)"

if [ $win_exit -ne 0 ] || [ $mac_exit -ne 0 ] || [ ${#missing[@]} -gt 0 ] || [ ${#debug[@]} -gt 0 ] || [ "$plist_version" != "$VERSION" ]; then
    [ $win_exit -ne 0 ] && tail -40 "$LOG-windows.log"
    [ $mac_exit -ne 0 ] && tail -40 "$LOG-macos.log"
    echo "EXPORT FAILED: godot exit windows $win_exit / macOS $mac_exit; missing: ${missing[*]:-none};" \
         "debug GodotSteam present: ${debug[*]:-none}; bundle version '$plist_version' (want $VERSION). Logs: $LOG-*.log"
    exit 1
fi

echo "Export OK: windows $(du -sh "$WIN" | cut -f1), macOS $(du -sh "$MAC" | cut -f1). Logs: $LOG-*.log"
