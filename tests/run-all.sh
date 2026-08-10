#!/usr/bin/env bash
# Run every .qgc regression test in tests/qgc/ and report each one's outcome.
#
#   tests/run-all.sh                  # all tests
#   tests/run-all.sh recycle shuffle  # only tests whose name contains one of these
#   tests/run-all.sh -v               # stream every test's full output instead of summarising
#
# Each test carries the args it needs on its own `#!args` line, so nothing has to be passed here —
# see tests/run-cli.sh. Sequential on purpose: concurrent Godot instances share this project's
# .godot/ import cache, so parallelism buys flakiness rather than speed.
#
# Env: GODOT=<binary> · QGC_TIMEOUT=<seconds> · QGC_VERBOSE=1
#
# Exit: 0 all passed · 1 one or more failed · 5 nothing to run / build failed
set -uo pipefail

PROJECT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
RUNNER="$PROJECT/tests/run-cli.sh"
VERBOSE="${QGC_VERBOSE:-0}"

if [ -t 1 ]; then
  GREEN=$'\033[32m'; RED=$'\033[31m'; DIM=$'\033[2m'; BOLD=$'\033[1m'; OFF=$'\033[0m'
else
  GREEN=''; RED=''; DIM=''; BOLD=''; OFF=''
fi

filters=()
for arg in "$@"; do
  case "$arg" in
    -v|--verbose) VERBOSE=1 ;;
    -*) echo "unknown option: $arg" >&2; exit 5 ;;
    *) filters+=("$arg") ;;
  esac
done

# Collect tests, keeping only those matching a filter (no filters = keep everything).
tests=()
for path in "$PROJECT"/tests/qgc/*.qgc; do
  [ -f "$path" ] || continue
  if [ ${#filters[@]} -eq 0 ]; then
    tests+=("$path")
    continue
  fi
  for f in "${filters[@]}"; do
    case "$(basename "$path")" in *"$f"*) tests+=("$path"); break ;; esac
  done
done

# A typo'd filter must not report a green zero-test run.
if [ ${#tests[@]} -eq 0 ]; then
  if [ ${#filters[@]} -eq 0 ]; then echo "no .qgc tests found in $PROJECT/tests/qgc" >&2
  else                              echo "no .qgc tests matched: ${filters[*]}" >&2
  fi
  exit 5
fi

# Build once here so the per-test runs don't each pay for it.
dotnet build "$PROJECT" -v q >/dev/null || { echo "build failed" >&2; exit 5; }
export QGC_SKIP_BUILD=1

LOGDIR="$(mktemp -d "${TMPDIR:-/tmp}/qgc.XXXXXX")"

# Widest name, so the verdict columns line up.
width=0
for path in "${tests[@]}"; do
  name="$(basename "$path" .qgc)"
  [ ${#name} -gt $width ] && width=${#name}
done

printf '%sqgc: %d test(s)%s\n\n' "$BOLD" "${#tests[@]}" "$OFF"

passed=0
failed_names=()

for path in "${tests[@]}"; do
  name="$(basename "$path" .qgc)"
  log="$LOGDIR/$name.log"
  start=$SECONDS

  if [ "$VERBOSE" = "1" ]; then
    printf '%s=== %s%s\n' "$DIM" "$name" "$OFF"
    "$RUNNER" "$path" 2>&1 | tee "$log"
    rc=${PIPESTATUS[0]}
  else
    "$RUNNER" "$path" >"$log" 2>&1
    rc=$?
  fi
  elapsed=$((SECONDS - start))

  # Exit codes are run-cli.sh's contract (see its header) — CliSession.Quit sets them.
  case $rc in
    0) label="PASS" ;;
    1) label="ASSERT" ;;
    2) label="ERROR" ;;
    5) label="MALFORMED" ;;
    124) label="TIMEOUT" ;;
    *) label="EXIT $rc" ;;
  esac

  # The run's own verdict line carries the assert/error counts and the seed it used.
  detail="$(grep -E '^(PASS|FAIL) ' "$log" | tail -1 | sed -E -e 's/^(PASS|FAIL) +//' -e 's/ +-> +exit [0-9]+$//')"
  [ -n "$detail" ] || detail="no verdict line — the run died before quitting"

  if [ $rc -eq 0 ]; then
    passed=$((passed + 1))
    printf '  %s%-9s%s  %-*s  %3ds  %s%s%s\n' "$GREEN" "$label" "$OFF" "$width" "$name" "$elapsed" "$DIM" "$detail" "$OFF"
  else
    failed_names+=("$name")
    printf '  %s%-9s%s  %-*s  %3ds  %s\n' "$RED" "$label" "$OFF" "$width" "$name" "$elapsed" "$detail"
    if [ "$VERBOSE" != "1" ]; then
      # Just the lines that say what went wrong: failed asserts, CLI errors, game errors. The final
      # `FAIL  N assert(s), ...` verdict is dropped — it is already the detail column above.
      grep -E '^(FAIL |ERR |!!!|HARD TIMEOUT)' "$log" \
        | grep -vE '^FAIL +[0-9]+ assert' | head -15 | sed 's/^/        /'
      printf '        %slog: %s%s\n' "$DIM" "$log" "$OFF"
    fi
  fi
done

total=${#tests[@]}
nfailed=$((total - passed))
printf '\n'
if [ $nfailed -eq 0 ]; then
  rm -rf "$LOGDIR"
  printf '%s%d passed, 0 failed%s\n' "$GREEN" "$passed" "$OFF"
  exit 0
fi
printf '%s%d passed, %d failed%s — %s\n' "$RED" "$passed" "$nfailed" "$OFF" "${failed_names[*]}"
printf '%slogs: %s%s\n' "$DIM" "$LOGDIR" "$OFF"
exit 1
