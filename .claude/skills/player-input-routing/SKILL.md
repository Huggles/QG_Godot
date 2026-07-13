---
name: player-input-routing
description: "Player input routing via InputRequest: ALL player selection requests (country, unit, battle target, card selection) MUST go through InputRequest subclasses and BroadCast() — never call SelectCountryHandler, SelectUnitHandler, or SelectBattleTargetHandler directly. Use when implementing any card step that requires player input, adding new selection types, or reviewing card execution code for missing network routing."
---

# Player Input Routing

## Core Rule

**Never call local handlers directly.** Every player selection in a card execution step must go through `InputRequest` and `.BroadCast()` so the correct multiplayer peer receives the request.

```
WRONG:  int id = await new SelectCountryHandler(ids).Handle();
RIGHT:  int id = (await new InputRequest.SelectCountryRequestHandler(Faction, ids).BroadCast()).ResponseCountryIds[0];
```

The `TargetFaction` on the `InputRequest` determines which peer runs `Handle()` — all others display "Waiting on X input…" via `Execute()`. Without `BroadCast()`, only the local peer shows the UI, breaking multiplayer.

---

## Available InputRequest Handlers

All defined as inner classes of `InputRequest` in `scenes/network/InputRequest.cs`.

| Handler class | Input params | Response field | Local handler used |
|---|---|---|---|
| `SelectCountryRequestHandler` | `Faction, List<int> countryIds` | `ResponseCountryIds[0]` | `SelectCountryHandler` |
| `SelectUnitRequestHandler` | `Faction, List<int> unitIds` | `ResponseUnitIds[0]` | `SelectUnitHandler` |
| `SelectBattleTargetRequestHandler` | `Faction, List<int> countryIds, List<int> unitIds` | See below | `SelectBattleTargetHandler` |
| `SelectBattleTargetRequestHandler` | `Faction, List<BattleTarget> targets` | See below | `SelectBattleTargetHandler` |
| `HandCardPlayRequestHandler` | `Faction` | `ResponseCardIds[0]` | Signal awaiter |
| `ActivateCardRequestHandler` | `Faction` | `ResponseCardIds[0]` | Signal awaiter |
| `HandCardsDiscardRequestHandler` | `Faction` | `ResponseCardIds` | `InputHandlerDiscardHand` |
| `CardsRequestHandler` | `Faction, List<int> cardIds` | `ResponseCardIds` | `InputHandlerDiscard` |
| `ForceDiscardHandCardsRequestHandler` | `Faction, int numberOfCards` | `ResponseCardIds` | `InputHandlerDiscard` |

### Decoding a BattleTarget response

```csharp
var resp = await new InputRequest.SelectBattleTargetRequestHandler(Faction, countryIds, unitIds).BroadCast();
BattleTarget target = resp.ResponseCountryIds.Count > 0
    ? new BattleTarget(resp.ResponseCountryIds[0], TargetType.COUNTRY)
    : new BattleTarget(resp.ResponseUnitIds[0], TargetType.UNIT);
```

The handler encodes the result into `ResponseCountryIds` for `TargetType.COUNTRY` or `ResponseUnitIds` for `TargetType.UNIT`.

---

## Adding a New Selection Type

When a card needs a player interaction that has no existing `InputRequest` handler:

1. **Add a local handler** in `scripts/C#/PlayerInputActions/` implementing `IGameEventHandler<T>`.
   - For selections involving Godot signals on `self`, extend `GodotObject`.
   - Local handler only runs on the peer that owns the UI — it is not network-aware.

2. **Add an `InputRequest` inner class** in `scenes/network/InputRequest.cs`:
   - Add a `[JsonDerivedType(typeof(MyRequestHandler), "MyDiscriminator")]` attribute on the `InputRequest` class.
   - Store all selection parameters in the base-class target fields (`TargetCountryIds`, `TargetUnitIds`, `TargetCardIds`, etc.) — these serialize over the network.
   - `Handle()` calls the local handler and stores the result in the appropriate `Response*` field(s).
   - **Prefer `TaskCompletionSource<T>`** over `GodotObject` + `[Signal]` when bridging multiple event sources into a single awaitable. It's pure C#, needs no `Free()`, and avoids the "object locked during EmitSignal" crash.
   - Only resort to `GodotObject` if you genuinely need Godot signal infrastructure (e.g. connecting from GDScript or the editor).

3. **Call `.BroadCast()` from card steps** — never call `.Handle()` directly from card execution code.

### InputRequest skeleton

```csharp
// 1. Add attribute above class declaration:
[JsonDerivedType(typeof(SelectFooRequestHandler), "SelectFoo")]

// 2. Inner class:
public class SelectFooRequestHandler : InputRequest
{
    // If you have MORE than one constructor, mark the one JSON should use:
    [JsonConstructor]
    public SelectFooRequestHandler(Faction targetFaction, List<int> targetIds) : base(targetFaction)
    {
        TargetCountryIds = targetIds; // or TargetUnitIds / TargetCardIds as appropriate
    }

    public override async Task Handle()
    {
        int result = await new SelectFooHandler(TargetCountryIds).Handle();
        ResponseCountryIds.Add(result); // or appropriate Response field
    }
}
```

> **JSON deserialization rule**: `System.Text.Json` handles a *single* parameterized constructor automatically. If a handler has **two or more constructors**, add `[JsonConstructor]` to the one whose parameters map to the base-class JSON properties (`TargetFaction`, `TargetCountryIds`, `TargetUnitIds`, etc.). Without it you get `NotSupportedException: Deserialization of types without a parameterless constructor…`.

---

## `Faction` in Card Execution Classes

All card logic classes expose a `Faction` property inherited from `CardLogic`. Use it directly as the `TargetFaction` — it is the faction that owns the card and should be doing the selecting.

This applies equally to:
- **Event cards** (`EventCardLogic`)
- **Status cards** (`StatusCardLogic`)
- **Response cards** (`ResponseCardLogic`)
- **Base action cards** (`CardLogic`)

---

## Files at a Glance

| File | Role |
|---|---|
| `scenes/network/InputRequest.cs` | All `InputRequest` subclasses + JSON polymorphism |
| `scripts/C#/PlayerInputActions/` | Local UI handlers (`SelectCountryHandler`, etc.) |
| `scenes/network/NetworkApi.cs` | `SendInputRequest()` — sends to peer, awaits response |
| `scripts/C#/Cards/ExecutionClasses/` | Card steps — must only use `InputRequest.BroadCast()` |
