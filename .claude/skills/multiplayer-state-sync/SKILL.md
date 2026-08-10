---
name: multiplayer-state-sync
description: "Multiplayer state synchronization: the replicated GameMessage stream (Pattern B) as primary approach, full snapshot (Pattern A) as initial state/fallback. Use when implementing or modifying multiplayer sync, adding a new GameMessage (ChangeEvent or PresentationEvent), or debugging state divergence between peers."
---

# Multiplayer State Synchronization

## Overview

Two patterns are used together:

| Pattern | When | What is sent |
|---------|------|--------------|
| **B — GameMessage replication** (primary) | Every game event during play | Message discriminator + constructor params (+ a state hash, for mutations) |
| **A — Full snapshot** (secondary) | Session start, late joiners | Serialized `MultiplayerGameStateSnapshot` |

Pattern B keeps clients in sync with minimal bandwidth. Pattern A delivers initial state.

> **Resync is not currently implemented.** A client that detects a hash mismatch logs a DESYNC block and
> calls `RequestResync`, but the server's snapshot reply is commented out in
> `NetworkApi.RequestResync`. A detected desync is a diagnostic, not a repair.

---

## The channel: `GameMessage`

`ChangeEventQueue` (autoload) — **not** any one message class — is the ordering primitive. An `Rpc` body
runs immediately in the receiving client's frame, while everything enqueued there is deferred and applied
serially. So a raw `Rpc` always risks landing ahead of the messages it belongs with. Anything that needs a
defined position in the stream must ride through the queue as a `GameMessage`.

`scripts/C#/GameMessages/` holds the channel; three branches ride it:

```
GameMessage                    Id (stream position), wire format, animation plumbing, history row
 ├── ChangeEvent               mutates authoritative state
 │                             → hashed, registered in CardPlayRound, CalculateAll(), divergence-tracked
 ├── PresentationEvent         mutates nothing; its whole effect is its animations
 │                             → never hashed, never pooled, no CalculateAll()
 └── RecalculateTagsMessage     delivers server-computed derived state; neither of the above
```

**Pick the branch by what the message does, not by what is convenient.** A message that only shows
something is a `PresentationEvent`. Giving it an empty `ExecuteAsync()` to make it a `ChangeEvent` would
buy a pointless `ComputeHash()` on every peer, a full per-faction tag recalculation, and — worst — make it
`CardPlayRound.LastChangeEvent`, which decides reaction request order and the `RequestBlock` self-block
guard. `PresentationEvent` cannot reach any of that.

### Flow

```
[Server]  message.Apply()                                  GameMessage.Apply → ApplyInternal
            ChangeEvent branch:
              register in CardPlayRound → BeforeAnimations
              → ExecuteAsync()                             ← the mutation
              → BroadCast()                                ← OnBeforeBroadcast stamps HashAfterApplication
              → CalculateAll()                             ← itself broadcasts a RecalculateTagsMessage
              → EmitSignal(ChangeEventApplied)
              → AfterAnimations → await Animation.Start()
              → GameMessages.Add(this), LatestAppliedId, GameChangeEventAfter
            PresentationEvent branch:
              → BroadCast() → Animations → await Animation.Start()
              → GameMessages.Add(this), GameChangeEventAfter

          BroadCast:  JsonSerializer.Serialize(ToDto(), typeof(GameMessageDto))
                      → NetworkApi.Rpc(ReceiveGameMessage, dtoJson)

[Client]  NetworkApi.ReceiveGameMessage(dtoJson)
            → Deserialize<GameMessageDto>()                ← polymorphic on "$type"
            → GameMessage.FromDto(dto)
            → if (msg is ChangeEvent ev) subscribe VerifyReplicatedHash
            → ChangeEventQueue.Enqueue(msg)                ← serial pump, wire order
            → next.Apply()                                 ← IsServer is false, so no re-broadcast
```

Note the broadcast happens **before** `CalculateAll()`, so the tags snapshot is enqueued on clients right
behind its own event and never applies to pre-change state.

Animations are **never serialized**. Each peer replays the message and constructs its own animation
objects; per-peer asymmetry comes from `ChangeEventAnimation.ForFactions` / `ForMyFaction`, not from the
wire. A headless server constructs none at all — `GameMessage.EnqueueAnimations` takes a *factory* so the
skip prevents construction, not just enqueueing (`ReturnCameraAnimation` reads
`InputManager.Current.Camera` in its constructor).

### `$type`: the one wire footgun

`GameMessageDto` is the **single** `[JsonPolymorphic]` root; every leaf DTO in all three branches registers
its discriminator there. `ChangeEventDto` and `PresentationEventDto` are plain intermediate classes with no
polymorphic attributes of their own — two configurations over the same leaf types is a silent-divergence
footgun.

`ChangeEvent` narrows `ToDto()` covariantly to `ChangeEventDto` for subclass convenience. That makes the
naive call dangerous:

```csharp
JsonSerializer.Serialize(ToDto())                         // ← TValue infers ChangeEventDto: NO "$type" emitted
JsonSerializer.Serialize(ToDto(), typeof(GameMessageDto)) // ← what BroadCast does. Correct.
```

System.Text.Json resolves polymorphism from the type it is *handed*, so inference off the narrowed override
produces a payload with no discriminator and every client throws `NotSupportedException` in `FromDto`.
Verified behaviour, not a theory — do not "simplify" that call.

### Adding a new GameMessage (checklist)

1. **Choose the branch.** Mutates state → `ChangeEvent`. Only presents → `PresentationEvent`.
2. Create the message class. `ChangeEvent` implements `ExecuteAsync()` + `Before`/`AfterAnimations`;
   `PresentationEvent` implements `Animations`.
3. Create a matching DTO subclassing `ChangeEventDto` or `PresentationEventDto`, carrying **only**
   constructor parameters — never a `GodotObject`. Live references travel as ids and are rehydrated by
   lookup (`ChangeEvent.ForId`, `CardState.ForId`).
4. Register `[JsonDerivedType(typeof(YourDto), "YourDiscriminator")]` on **`GameMessageDto`**, under the
   right section comment.
5. Implement `ToDto()` using `ChangeEventDto.Build<T>(this, Id)` or
   `PresentationEventDto.Build<T>(this, Id)` — those copy the base fields for you.
6. Add an arm to `GameMessage.FromDto()`, in the matching section.
7. If a field must survive the round trip but is not a constructor parameter, override `ApplyDtoFields`.

Steps 4 and 6 are two places that must stay in sync; a missing arm throws `NotSupportedException` on every
client (caught and reported, not fatal).

### State hash

`MultiplayerGameState.ComputeHash()` (`scenes/MultiplayerGameState.cs`) is FNV-1a 32 over a
deterministically ordered string: country unit counts, unit `CountryId`/`ImmuneForTurn`/`SuppliedForTurn`,
strait `ControllingCountryId`, and per playable faction `Score` + deck-pile counts/ids. Order must be
identical on every peer — every enumeration is explicitly `OrderBy`'d.

Stamped in `ChangeEvent.OnBeforeBroadcast()`, compared in `NetworkApi.VerifyReplicatedHash`.

**Deliberately outside the hash:** computed tags (derived, delivered separately), and any state a peer can
only learn from the wire — `GameFlow.TurnStep`, `CardPlayRounds`, `CardState.ActivatedInTurns`,
`VictoryPointSummaries`. The rule: the hash covers state a peer could independently get *wrong*, not
everything that is mutable. Note this means several `ChangeEvent`s legitimately leave the hash unchanged.

---

## Pattern A — Full snapshot (initial state / fallback)

### Critical rule: always update in place

**Never replace a `StateObject` instance.** `CountryState`, `UnitState`, `CardState`, `StraightState`,
`FactionState` extend `GodotObject` and hold scene-node references, constructor-wired signal subscriptions,
and tag callbacks driving visuals. Replacing them breaks all of it. `ApplySnapshot` mutates the existing
objects' fields, and `DeckState` lists are `Clear()`+`AddRange()`d rather than reassigned.

### Shape

`MultiplayerGameState` holds the live `GodotObject` lists. `MultiplayerGameStateSnapshot` is the wire
format — plain POCOs keyed by `Id`: `CountryStateDto`, `UnitStateDto`, `StraightStateDto`,
`FactionStateDto` (with a nested `DeckStateDto`), plus a `ComputedTagsSnapshot`.

There is **no `CardStateDto`**: card location lives in the faction's `DeckState` id lists, not on the card.

Static/immutable data (country names, unit factions, card data) is not included — it is loaded from data
files at startup on every peer.

Tags **are** in the snapshot, as server-computed state that clients apply directly via
`GameStateCalculator.ApplyComputedTags` rather than recalculating independently.
`GameStateCalculator.CalculateAll()` is server-only.

### Adding a new state field to snapshots (checklist)

1. Identify the `StateObject` that owns it.
2. Add the field to the corresponding DTO in `scenes/MultiplayerGameState.cs`.
3. Populate it in `BuildSnapshot()`.
4. Write it back **in place** in `ApplySnapshot()`.
5. Decide whether `ComputeHash()` should cover it — yes if a peer could compute it wrong; no if it can only
   arrive over the wire.

Do **not** put in DTOs: `GodotObject`/scene-node references, signal delegates, derived properties, tag state.

---

## Key files

| File | Role |
|------|------|
| `scripts/C#/GameMessages/GameMessage.cs` | Channel base: `Id`, `Apply()`, `BroadCast()`, `FromDto()`, `EnqueueAnimations` |
| `scripts/C#/GameMessages/GameMessageDto.cs` | The single `[JsonPolymorphic]` root — register every discriminator here |
| `scripts/C#/GameMessages/PresentationEvent.cs` | Presentation-only branch |
| `scripts/C#/GameMessages/RecalculateTagsMessage.cs` | Derived-state delivery |
| `scripts/C#/ChangeEvents/ChangeEvent.cs` | Mutation branch: `ExecuteAsync`, hash, pool registration, divergence tracking |
| `scripts/C#/ChangeEvents/ChangeEventDto.cs` | Mutation DTOs + `Build<T>` |
| `scenes/network/ChangeEventQueue.cs` | The serial pump — the actual ordering primitive |
| `scripts/C#/Utils/NetworkApi.cs` | `ReceiveGameMessage`, `VerifyReplicatedHash`, `RequestResync`, `ReceiveFullSnapshot`, input-request RPCs |
| `scenes/MultiplayerGameState.cs` | Live state, `BuildSnapshot`/`ApplySnapshot`/`ComputeHash`, snapshot DTOs |
| `scripts/C#/DataObjects/GameStateCalculator.cs` | `CalculateAll()` (server-only), `ApplyComputedTags`, `BuildTagsSnapshot` |

---

## Debugging state divergence

1. **Hash mismatch logged?** The DESYNC block from `NetworkApi.VerifyReplicatedHash` names the event that
   caused the split and dumps the full client state. Remember no repair follows it.
2. **Client threw in `FromDto`?** A missing `[JsonDerivedType]` or a missing `FromDto` arm. Reported via
   `ErrorReporter`, not fatal, but the message is silently skipped.
3. **No `$type` in the payload?** Something serialized against a narrowed DTO type. See the footgun above.
4. **Message applied but a field is wrong?** It is not a constructor parameter and not restored in
   `ApplyDtoFields`. `ForceDiscardCardsChangeEvent.ModifiersApplied` is the worked example of a field the
   client must *not* recompute.
5. **A dropped message.** `ChangeEventQueue.ProcessQueue` catches per item to keep the pump alive. For a
   `ChangeEvent` the hash check catches the resulting desync; for a `PresentationEvent` nothing does — a
   dropped one is a silently missing animation.
6. **Tags stale?** `CalculateAll()` is server-only; clients only ever get tags via `RecalculateTagsMessage`
   or a snapshot's `ComputedTags`.
7. **Ordering looks wrong?** Check whether the thing that jumped the queue is a raw `Rpc`. If it needs a
   position in the stream, it should be a `GameMessage`.
