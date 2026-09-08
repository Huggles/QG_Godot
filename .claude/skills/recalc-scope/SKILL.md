---
name: recalc-scope
description: "Which ChangeEvents declare which RecalcScope and why: the Board/Decks/Flow sources of derived state, the reasoning that justifies each narrowing, and how to verify one. Use when adding a ChangeEvent, changing an existing one's ExecuteAsync, narrowing a scope for performance, or debugging a tag that is stale rather than wrong."
---

# RecalcScope: what each ChangeEvent invalidates

Every `ChangeEvent` runs `GameStateCalculator.CalculateAll(RecalcScope)` after it mutates
(`ChangeEvent.cs:204`). The scope says **what the event changed**, not which phases to run — the phases
are `CalculateAll`'s business, because the dependency order between them is real.

`scripts/C#/Enums/RecalcScope.cs` holds the enum and the rationale; this skill holds the per-event map.

## The three sources

| Flag | The source that moved | What has to be re-derived |
|---|---|---|
| `Board` | Unit positions, country occupancy, strait control, per-turn supply | supply, attackable, buildable, recruitable |
| `Decks` | A card moved between piles (hand/deck/discard/status/response) or in or out of the play pool | which cards count as played |
| `Flow` | Turn, round, step, trigger context, per-step play counters | step executability, card availability |
| `None` | Nothing derived reads it | nothing — `CalculateAll` returns immediately |
| `All` | The default | everything |

The phase chain inside `CalculateAll` is `board → playedCards → executableSteps → cardTags`. Only the two
**heads** are optional; the tail runs whenever anything at all moved. That is why a scope names the source
rather than a phase list.

## The map

Five events are narrowed. Everything else is `All` by inheritance.

| Event | Scope | Why |
|---|---|---|
| `ScorePointsChangeEvent` | `None` | Moves `FactionState.Score`, appends to `GameFlow.VictoryPointSummaries` (a log the victory screen reads at the end), emits a UI signal. **No Condition, tag or card script reads any of the three.** One of the most frequent events in a game. |
| `ChangeStepChangeEvent` | `Decks \| Flow` | `Flow` because card conditions read `GameFlow.TurnStep`. `Decks` because it opens a fresh `CardPlayRound`, and an empty `CardPool` changes what `CalculatePlayedCardsForFaction` counts. **Not `Board`** — and that matters most here: this is over half of all events in a game. |
| `PlayCardChangeEvent` | `Decks \| Flow` | `DeckState.PlayCard` moves the card out of hand into the pool or discard (`Decks`); it bumps `CardsPlayedThisTurnStep` and stamps `PlayedInTurn`, both read by conditions (`Flow`). |
| `DrawCardsChangeEvent` | `Decks` | Cards move draw-pile → hand, and on a reshuffle discard → draw pile. Piles only; no piece moves, no step or trigger context shifts. |
| `SpendPlayActionChangeEvent` | `Flow` | Increments the per-step play counter and nothing else. |

### Deliberately left at `All`

- **`ChangeRoundChangeEvent`** — looks like a pure `Flow` event and is not. Its `ResetPerTurnState` clears
  `SuppliedForTurn` on units (`Board`) and re-arms card steps. Leave it.
- Everything not in the table above. Being unannotated is not an oversight to be tidied up; it is the
  default doing its job.

### The structural fact behind the table

`CalculatePlayedCardsForFaction` used to live inside the board group. It reads **deck piles**, so it now
runs under `Decks`. If it were still grouped with the board, every draw, play and discard — about a
quarter of all events — would re-derive supply for six factions to answer a question about cards.

## Deciding the scope for a new event

1. **Default to `All` by writing nothing.** An override is a claim you have to be able to defend.
2. **Describe only your own `ExecuteAsync`.** Whatever a card goes on to *cause* arrives as its own nested
   `ChangeEvent`s, each carrying its own scope — a card that deploys a unit raises a
   `DeployUnitChangeEvent`, and that one is `Board`. Scopes compose; nobody has to reason about the chain.
3. **Read the mutation line by line and ask which of the three sources each line touches.** Two of the five
   annotations above came out wider than a first guess (`ChangeStepChangeEvent` also opens a
   `CardPlayRound`; `PlayCardChangeEvent` touches both piles and counters). Assume you will be surprised.
4. **Write the reason in a doc comment on the override**, not just the value. The value alone is
   unreviewable a year later.

Plausible-looking candidates that have **not** been verified and so are still `All`:
`GrantSupplyChangeEvent` (sets `SuppliedForTurn` → looks like `Board`), `SetStartingScoreChangeEvent`
(looks like `None`, same argument as `ScorePoints`), `ReorderDeckChangeEvent` (changes deck order, not pile
membership). Do not annotate any of them from this paragraph — do step 3 first.

## Why the default must stay `All`

The opposite arrangement — a central list enumerating everything that could invalidate a tag — cannot be
kept correct: **67 `CustomCondition` lambdas** in the card scripts read whatever they like. Opt-in
narrowing keeps each claim small, local and reviewable, and keeps an event written by someone who has
never read any of this correct by default.

## Non-ChangeEvent call sites

Three sites are narrowed to `RecalcScope.Flow` — `CardPlayRound.cs:483`, `:538`, `:621`. Their own comments
say they refresh trigger context and never the board.

Three are deliberately **not** narrowed: `CardStep.cs:200`, `SupplyStepHandlerDefault.cs:43`,
`StepMutatorRunner.cs:77`. A step body can mutate outside a `ChangeEvent`, and the mutator one is
documented as load-bearing for fresh supply tags.

## Verifying a scope change

A wrong scope **leaves a stale tag; it does not throw.** Nothing fails fast, so a build-and-eyeball is not
evidence. Use the before/after stash diff (see the `transcript-diff-regression` memory):

1. Run a block of sim seeds on the new build, capturing the `game_result` lines.
2. `git stash push -u` the change; rebuild.
3. Run the **same** seeds; capture again.
4. `diff` the two. Byte-identical across the block, with no new stalls, is the pass condition.
5. `git stash pop`, rebuild.

Thirty seeds is a reasonable block. Do not substitute the `.qgc` suite — per the standing project rule,
`tests/run-all.sh` and `tests/run-cli.sh` are never run.

## Debugging a suspected stale tag

Symptom: a card or step is offered when it should not be, or missing when it should be there, and the
underlying state is correct. Set the suspect event's `RecalcScope` back to `All`. If the symptom goes away,
the annotation is wrong — widen it and fix the doc comment. `CalculateAll` logs its scope
(`Calculating game state for all factions (scope ...)`) under `DebugUtilities.PrintPeer`.
