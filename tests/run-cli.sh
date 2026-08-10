#!/usr/bin/env bash
# Run a .qgc command script headlessly and exit with its verdict.
#
#   tests/run-cli.sh tests/qgc/opening.qgc [extra=args ...]
#   tests/run-cli.sh --interactive [extra=args ...]
#   tests/run-all.sh                              # every test in tests/qgc/
#
# A .qgc file is just the CLI's command stream, so it is fed in on stdin — the same path an
# interactive session uses. That is why what you type is exactly what replays.
#
# The args a test needs (scenario, seed) live in the test itself, on a `#!args` line:
#
#   #!args scenario=Scenario_OneRound seed=42
#
# so `tests/run-cli.sh <file>` is the whole command. That line is a comment to the interpreter, so
# the file still replays byte-for-byte when piped by hand. A file without one just gets whatever is
# passed here. Extra args on the command line are appended, and so win: CliArgs is last-one-wins on
# a duplicate key (CliArgs.cs:27).
#
# Env: GODOT=<binary> · QGC_TIMEOUT=<seconds> · QGC_SKIP_BUILD=1 (caller already built)
#
# Exit: 0 pass · 1 assertion failed · 2 game error · 5 malformed assertion · 124 hard timeout
set -uo pipefail

PROJECT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
GODOT="${GODOT:-/Applications/Godot_mono.app/Contents/MacOS/Godot}"
HARD_TIMEOUT="${QGC_TIMEOUT:-120}"

[ -x "$GODOT" ] || { echo "Godot not found at $GODOT (override with GODOT=...)" >&2; exit 5; }

if [ "${QGC_SKIP_BUILD:-}" != "1" ]; then
  dotnet build "$PROJECT" -v q >/dev/null || { echo "build failed" >&2; exit 5; }
fi

SCRIPT="${1:---interactive}"
shift || true

run() { "$GODOT" --headless --path "$PROJECT" -- cli=true "$@"; }

if [ "$SCRIPT" = "--interactive" ]; then
  run "$@"
  rc=$?
else
  [ -f "$SCRIPT" ] || { echo "no such script: $SCRIPT" >&2; exit 5; }

  # Prepend the test's own `#!args`, so explicit args stay last and keep overriding. Unquoted on
  # purpose: every token is a space-free key=value, and `set --` needs them as separate words.
  directive="$(sed -n 's/^#!args[[:space:]]\{1,\}//p' "$SCRIPT" | head -1)"
  # shellcheck disable=SC2086
  [ -n "$directive" ] && set -- $directive "$@"

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
