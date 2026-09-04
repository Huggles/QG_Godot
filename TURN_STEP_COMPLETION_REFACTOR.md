# Deferred refactor: turn-step completion as awaited Tasks

**Status:** proposed, not done. Written while implementing step mutators, which worked around the problem
described here rather than fixing it.

## The problem

`GameFlow` advances through seven turn steps, but the six that do work signal completion in **three
different ways**:

| Step | Handler | Completion idiom |
|---|---|---|
| START | none — finishes inline | n/a (raises no prompt; see `GameFlow.StartTurnStepBody`) |
| PLAY_CARD | `PlayStepHandlerDefault` | Godot `[Signal] PlayStepFinished` |
| SUPPLY | `SupplyStepHandlerDefault` | C# `event Action SupplyStepFinished` |
| VICTORY_POINT | `VictoryStepHandlerDefault` | none — plain awaited `Task` |
| DISCARD | `DiscardStepHandlerDefault` | C# `event Action DiscardStepFinished` |
| DRAW | `DrawStepHandlerDefault` | C# `event Action DrawStepFinished` |

Each step method starts its handler and returns immediately; the handler later fires its event, which calls
back into `GameFlow` to advance the loop. So `Func<Task> GameTurnStep.Handler` completes long before the step
it names actually finishes.

The worst of it is PLAY_CARD, which does not use its own signal to detect completion — it subscribes to the
**globally shared** `EventBus.CardPlayPoolFinished`, emitted by `CardPlayRound.Finish()`. START used to
subscribe to the same signal for its own card round; that round is gone (the "beginning of your turn" cards
moved into the play prompt), so there is one listener now rather than two — but the hazard below survives,
because an aborted play step's leftover subscription still fires on the next round to finish:

- `GameFlow.stepEpoch` and the stale-epoch gate in `StartNextStep` — without it, an aborted step's leftover
  subscription fires on the *next* round to finish and silently skips a step.
- `GameFlow.CancelCurrentStepHandler()`, which detaches every handler and every `GameFlow` subscription.
- `PlayStepHandlerDefault.Cancel()` and its `subscribed` bookkeeping.

The comments at `GameFlow.cs:190-194` and `PlayStepHandlerDefault.cs:29-33` document the bugs that produced
each of these.

## The proposal

Make every step handler return a `Task` that completes when the step is genuinely over. The whole loop then
collapses into one readable method:

```csharp
private async Task RunTurnStep(GameTurnStep step)
{
    await new ChangeStepChangeEvent(step.TurnStep).ApplyChange();
    await StepMutatorRunner.Run(step.TurnStep, MutatorTiming.BEFORE, CurrentFaction);
    await step.Handler();                                              // completes at step end
    await StepMutatorRunner.Run(step.TurnStep, MutatorTiming.AFTER, CurrentFaction);
    StartNextStep();
}
```

The card-play handlers lose their entire reason for existing — `CardPlayRound.Start` is already an awaitable
`Task`, so the EventBus round-trip is pure indirection:

```csharp
// PlayStepHandlerDefault, in full
public Task Start(Faction faction) => CardPlayRound.Current.Start(faction);
```

## What it buys

- **Symmetric hooks.** The mutator system currently threads the completed `TurnStep` through a
  `FinishStep(step)` call at six sites, because the callback is the only place that knows which step just
  ended. With an awaited handler the step is simply in scope.
- **Deletes the zombie-subscription hazard**, and with it `stepEpoch`, `CancelCurrentStepHandler`, both
  `Cancel()` methods and the `subscribed` flags. A step that throws unwinds through the `await` instead of
  leaving a live subscription behind.
- **One completion idiom** instead of three.
- Exceptions propagate to a single `Guard.FireAndForget` at the top of the loop rather than being
  reported per-handler.

## What it costs

- Touches all six step handlers and both handler interfaces.
- Touches the error-recovery path: `ResumeAfterFailure` bumps `TurnStepCounter` directly and bypasses
  `StartNextStep` entirely, and `ErrorReporter.CancelPendingAwaiters` sweeps the step handlers by field.
  Both need rethinking once completion is an await rather than an event.
- `TurnStepCounter` is a replicated `[Export]` whose **setter** dispatches the handler, and clients run that
  setter too (gated on `Multiplayer.IsServer()` for the handler, but still emitting `NextStepStarted`). The
  advance mechanism itself must stay exactly as it is; only what the handler *does* changes.
- `StartNewTurn` (TurnStep.END) is asymmetric — no `ChangeStepChangeEvent`, and it resets the counter rather
  than incrementing it. It needs to stay a special case.

## Why it was deferred

The step-mutator work needed a hook, not a rewrite. `FinishStep(TurnStep)` gets the same behaviour with a
change confined to `GameFlow`, whereas this refactor rewrites the completion contract of every step handler
and the error-recovery paths that depend on it. Worth doing on its own, with the no-mutator regression pass
(every step fires exactly once per turn) as the acceptance test.
