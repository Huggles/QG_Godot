# qgsim — parallel sim orchestrator

Runs the game's headless `sim=true` mode across many seeds at once and aggregates the results into
balance statistics.

```sh
cd sim
dotnet build QGSim.csproj

./bin/Debug/net8.0/qgsim.exe --seeds 42,1,7 --decision-seeds 1-40
```

`--help` lists every option.

## Why one process per game

The game cannot run two games in one process, so the orchestrator is a process pool rather than a
thread pool. This is forced, not a preference:

| Blocker | Where |
|---|---|
| `static Random`, `static Seed`, `static DrawCount` | `scripts/C#/Utils/GameRandom.cs:16-24` |
| One static input-provider slot | `scripts/C#/Presentation/InputServices.cs:11-20` |
| 12 Godot autoloads, one `SceneTree` | `project.godot` `[autoload]` |
| `PendingScenarioPath` / `PendingSeed` statics | `scripts/C#/Game/GameManager.cs:37-45` |
| `CliSession.Quit(code)` ends the **process** | `scripts/C#/Cli/CliSession.cs:253` |

`GameRandom` alone settles it: two games sharing one static `Random` would interleave their draws and
stop reproducing from `(seed, decision_seed)` — and reproducibility is the whole point of the sample.

## Concurrency, measured

The `.godot/` import cache is shared between workers, and `tests/run-all.sh` avoids concurrency for
exactly that reason. Measured here, that concern does **not** materialise for `--headless` runs
against a warm cache: results are byte-identical at every worker count tested.

16 games of `Scenario_Basic`, 16-core machine:

| workers | 2 | 4 | 6 | 8 | 12 | 16 |
|---|---|---|---|---|---|---|
| wall | 22.9s | 13.6s | 10.4s | 9.2s | 9.2s | 9.0s |

Flat from 8 onward — each worker already fans out internally (`GameStateCalculator.CalculateAll` runs
a `Parallel.ForEach` over six factions ~1000× per game), so extra workers only trade cache for context
switches. The default is **half the cores**, which buys all the available speedup and leaves the
machine usable. In practice: 120 games in 66s, versus roughly five minutes sequentially.

If a future change does introduce cache contention, the fallback is to export a headless build once
and point `--godot` at it — an exported `.pck` has no import cache to contend on.

## Output

Each run writes to `sim/runs/<timestamp>/` (gitignored):

- **`results.jsonl`** — one `game_result` per line, **sorted by `(scenario, seed, decision_seed)`**.
  `game_result` carries no wall-clock, so the same batch renders the same bytes every time; sorting
  extends that from one line to the whole file, which makes a batch diffable as a balance baseline.
  Wall-clock lives in the `sim_perf` event, deliberately kept out.
- **`summary.txt`** — win rates, average score by faction, end reasons.
- **`failures.txt`** — every non-clean run with its exact re-run arguments.
- **`logs/`** — raw stdout per run. Clean runs are pruned unless `--keep-logs`; failures always kept.

## Outcomes

The CLI's three exit codes are kept distinct rather than collapsed into pass/fail, because they need
different responses:

| Outcome | Exit | Meaning |
|---|---|---|
| `clean` | 0 | Win condition reached, no errors |
| `result+errors` | 2 | A result was produced, but rules errors occurred on the way |
| `stalled` | 3 | In-game watchdog fired, no result |
| `timeout` | — | Killed by the orchestrator before the in-game watchdog could fire |
| `crashed` | other | Failed to launch, or an exit code the CLI never emits |

Only runs that produced a result feed the balance statistics. Folding a stall in as a loss for
whichever side was behind is the exact miscount the separate exit code 3 exists to prevent.

`qgsim` itself exits `0` when every run was clean, `1` when any run needs attention, `2` when the
batch could not run at all.

## Notes

- Needs the `_console` Godot build — the plain Windows binary writes nothing to stdout. Override the
  path with `--godot` or the `QG_GODOT` environment variable.
- Builds the game assembly once up front; `--skip-build` skips it. Building per worker would turn one
  compile error into hundreds of crashed runs.
- Ctrl+C stops scheduling, kills in-flight jobs, and still writes the results that completed.
- This project is **not** in `Quartermaster General.sln` (that solution carries Godot's
  `ExportDebug`/`ExportRelease` configurations, which this app does not define), and `sim/` is excluded
  from the game `.csproj` plus carries a `.gdignore`. Build it by path.
