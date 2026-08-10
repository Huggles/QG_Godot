#!/usr/bin/env bash
# Run a .qgc command script headlessly and exit with its verdict.
#
#   tests/run-cli.sh tests/qgc/opening.qgc [extra=args ...]
#   tests/run-cli.sh --interactive [extra=args ...]
#
# A .qgc file is just the CLI's command stream, so it is fed in on stdin — the same path an
# interactive session uses. That is why what you type is exactly what replays.
#
# Exit: 0 pass · 1 assertion failed · 2 game error · 5 malformed assertion · 124 hard timeout
set -uo pipefail

PROJECT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
GODOT="${GODOT:-/Applications/Godot_mono.app/Contents/MacOS/Godot}"
HARD_TIMEOUT="${QGC_TIMEOUT:-120}"

[ -x "$GODOT" ] || { echo "Godot not found at $GODOT (override with GODOT=...)" >&2; exit 5; }

dotnet build "$PROJECT" -v q >/dev/null || { echo "build failed" >&2; exit 5; }

SCRIPT="${1:---interactive}"
shift || true

run() { "$GODOT" --headless --path "$PROJECT" -- cli=true "$@"; }

if [ "$SCRIPT" = "--interactive" ]; then
  run "$@"
  rc=$?
else
  [ -f "$SCRIPT" ] || { echo "no such script: $SCRIPT" >&2; exit 5; }

  # `timeout` is not on macOS by default, so back the run with a killer subshell. The redirect must
  # be attached to the backgrounded command itself: bash gives background jobs /dev/null for stdin,
  # so redirecting the wrapper instead silently feeds the game an empty script.
  run "$@" < "$SCRIPT" &
  pid=$!
  ( sleep "$HARD_TIMEOUT"; kill -9 "$pid" 2>/dev/null ) &
  killer=$!
  wait "$pid"; rc=$?
  kill "$killer" 2>/dev/null
fi
[ $rc -eq 137 ] && { echo "HARD TIMEOUT after ${HARD_TIMEOUT}s" >&2; exit 124; }
exit $rc
