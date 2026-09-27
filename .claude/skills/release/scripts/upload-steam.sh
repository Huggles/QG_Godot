#!/usr/bin/env bash
# Uploads Builds/windows and Builds/macOS to their depots as one Steam build, with steamcmd
# ($STEAMCMD, else ~/Steam/steamcmd.sh) and its cached login for $STEAM_BUILD_USER.
# Usage: upload-steam.sh X.Y.Z [SET_LIVE]   SET_LIVE defaults to '' (upload only). Steam refuses SetLive "default"
# from a build script for this app (the commit fails), so default goes live by hand in Steamworks.
set -u
VERSION="${1:?usage: upload-steam.sh X.Y.Z [SET_LIVE]}"
SET_LIVE="${2:-}"
ROOT="$(cd "$(dirname "$0")/../../../.." && pwd)"
STEAMCMD="${STEAMCMD:-$HOME/Steam/steamcmd.sh}"
WORK="$ROOT/Builds/steam"
[ -n "${STEAM_BUILD_USER:-}" ] || { echo 'STEAM_BUILD_USER is not set.'; exit 1; }

mkdir -p "$WORK/output"
VDF="$WORK/app_build_5159090.vdf"
sed -e "s|{{DESC}}|v$VERSION|" -e "s|{{CONTENT_ROOT}}|$ROOT/Builds|" \
    -e "s|{{BUILD_OUTPUT}}|$WORK/output|" -e "s|{{SET_LIVE}}|$SET_LIVE|" \
    "$(dirname "$0")/../steam/app_build.vdf" > "$VDF"

# NoPromptForPassword makes a missing cached login fail fast instead of waiting on a prompt nobody sees.
out="$("$STEAMCMD" +@NoPromptForPassword 1 +login "$STEAM_BUILD_USER" +run_app_build "$VDF" +quit 2>&1)"
grep -E 'ERROR|Fail|Success|BuildID|Logged in|password|Steam Guard' <<< "$out"

build_id="$(sed -nE 's/.*Successfully finished AppID [0-9]+ build \(BuildID ([0-9]+)\).*/\1/p' <<< "$out" | head -1)"
if [ -z "$build_id" ]; then
    tail -20 <<< "$out"
    echo "STEAM UPLOAD FAILED. Full steamcmd output is in $WORK/output."
    exit 1
fi
echo "Steam upload OK: BuildID $build_id"
if [ -n "$SET_LIVE" ] && grep -iE 'set.*live|SetLive' <<< "$out" | grep -qE 'ERROR|Fail|denied|not allowed'; then
    echo "SETLIVE REFUSED: set the build live by hand at https://partner.steamgames.com/apps/builds/5159090"
    exit 2
fi
