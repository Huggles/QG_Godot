---
name: multiplayer-state-sync
description: "Multiplayer state synchronization: ChangeEvent replication (Pattern B) as primary approach, full GameState snapshot (Pattern A) as fallback/resync. Use when implementing or modifying multiplayer sync, adding new ChangeEvents or state fields, or debugging state divergence between peers."
---

# Multiplayer State Synchronization

## Overview

Two patterns are used together:

| Pattern | When | What is sent |
|---------|------|--------------|
| **B — ChangeEvent replication** (primary) | Every game event during play | ChangeEvent type + constructor params + state hash |
| **A — Full snapshot** (secondary) | Session start, late joiners, hash mismatch resync | Full serialized `MultiplayerGameState` |

Pattern B keeps clients in sync with minimal bandwidth. Pattern A is the safety net and the initial state delivery mechanism.

---

## Pattern B — ChangeEvent Replication (Primary)

The server applies each `ChangeEvent` locally, then broadcasts just its type and constructor parameters. Clients reconstruct the identical event and apply it to their own local state. A state hash is appended so clients can detect any divergence.

```
[Server] ChangeEvent.ApplyChange()
           → ToDto() → ChangeEventDto { "$type": "DeployUnit", countryId, faction, ... }
           → ComputeStateHash()
           → Rpc(ReceiveChangeEvent, dtoJson, hash)

[Client] ReceiveChangeEvent(dtoJson, hash)
           → JsonSerializer.Deserialize<ChangeEventDto>() (polymorphic)
           → ChangeEvent.FromDto(dto) → reconstruct event
           → ApplyChange(localOnly: true)  ← does NOT re-broadcast
           → ComputeStateHash()
           → if hash mismatch → Rpc(RequestResync) back to server
```

### ChangeEvent DTOs

Each `ChangeEvent` needs a matching DTO class containing only its constructor parameters. The hierarchy uses `[JsonPolymorphic]` for one-call serialize/deserialize:

```csharp
// ChangeEventDto.cs (new file)
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(DeployUnitChangeEventDto),   "DeployUnit")]
[JsonDerivedType(typeof(RemoveUnitChangeEventDto),   "RemoveUnit")]
[JsonDerivedType(typeof(PlayCardChangeEventDto),     "PlayCard")]
[JsonDerivedType(typeof(ActivateReactionChangeEventDto), "ActivateReaction")]
[JsonDerivedType(typeof(DiscardCardsChangeEventDto), "DiscardCards")]
// register every ChangeEvent subclass here
public abstract class ChangeEventDto
{
    public Faction TriggeringFaction { get; set; }
    public int     SourceCardId      { get; set; } = -1;
    public bool    IsTrigger         { get; set; } = true;
}

public class DeployUnitChangeEventDto : ChangeEventDto
{
    public int        CountryId      { get; set; }
    public DeployType DeploymentType { get; set; }
}

public class RemoveUnitChangeEventDto : ChangeEventDto
{
    public int               UnitId { get; set; }
    public UnitRemovalReason Reason { get; set; }
}

public class PlayCardChangeEventDto : ChangeEventDto { }          // TriggeringFaction + SourceCardId is enough

public class ActivateReactionChangeEventDto : ChangeEventDto
{
    public int SourceChangeEventId { get; set; }
}

public class DiscardCardsChangeEventDto : ChangeEventDto
{
    public Faction TargetFaction  { get; set; }
    public int     NumberOfCards  { get; set; }
}
```

### ToDto / FromDto on ChangeEvent

Add two abstract members to `ChangeEvent`:

```csharp
// ChangeEvent.cs
public abstract ChangeEventDto ToDto();

public static ChangeEvent FromDto(ChangeEventDto dto)
{
    ChangeEvent ev = dto switch
    {
        DeployUnitChangeEventDto d   => new DeployUnitChangeEvent(d.TriggeringFaction, d.CountryId, d.DeploymentType),
        RemoveUnitChangeEventDto d   => new RemoveUnitChangeEvent(d.TriggeringFaction, d.UnitId, d.Reason),
        PlayCardChangeEventDto d     => new PlayCardChangeEvent(d.TriggeringFaction, d.SourceCardId),
        ActivateReactionChangeEventDto d => new ActivateReactionChangeEvent(
                                            d.TriggeringFaction, d.SourceCardId,
                                            ChangeEvent.ForId(d.SourceChangeEventId)),
        DiscardCardsChangeEventDto d => new DiscardCardsChangeEvent(d.TriggeringFaction, d.TargetFaction, d.NumberOfCards),
        _ => throw new NotSupportedException($"Unknown ChangeEventDto type: {dto.GetType().Name}")
    };
    ev.IsTrigger = dto.IsTrigger;
    return ev;
}
```

Each concrete `ChangeEvent` implements `ToDto()`:

```csharp
// DeployUnitChangeEvent.cs
public override ChangeEventDto ToDto() => new DeployUnitChangeEventDto
{
    TriggeringFaction = TriggeringFaction,
    SourceCardId      = SourceCardId,
    IsTrigger         = IsTrigger,
    CountryId         = CountryId,
    DeploymentType    = DeploymentType
};
```

### State Hash

Compute a deterministic hash over all mutable state after each event. The field order and sort order must be identical on all peers:

```csharp
// GameState.cs (or a static GameStateHasher helper)
public string ComputeHash()
{
    var sb = new System.Text.StringBuilder();

    foreach (var cs in CountryStates.OrderBy(c => c.Id))
        sb.Append($"C{cs.Id}:{string.Join(",", cs.Units.OrderBy(kv => (int)kv.Key).Select(kv => $"{(int)kv.Key}={kv.Value}"))}|");

    foreach (var us in UnitStates.OrderBy(u => u.Id))
        sb.Append($"U{us.Id}:{us.CountryId},{us.ImmuneForTurn}|");

    foreach (var cs in CardStates.OrderBy(c => c.Id))
        sb.Append($"K{cs.Id}:{(int)cs.Zone}|");

    foreach (var ss in StraightStates.OrderBy(s => s.Id))
        sb.Append($"S{ss.Id}:{ss.ControllingCountryId}|");

    return sb.ToString().GetHashCode().ToString("X8");
}
```

Tags are **not** included in the hash — they are derived state recomputed by `GameStateCalculator`.

### ApplyChange with localOnly flag

Add a `localOnly` parameter to `ApplyChange()` so clients can apply a replicated event without re-broadcasting:

```csharp
// ChangeEvent.cs
public async Task<bool> ApplyChange(bool localOnly = false)
{
    EventBus.Emit(EventBus.SignalName.GameChangeEventBefore);
    await ExecuteAsync();
    GameStateCalculator.CalculateAll();

    if (Multiplayer.IsServer() && !localOnly)
    {
        string dtoJson  = JsonSerializer.Serialize(ToDto());
        string hash     = GameSession.Current.GameState.ComputeHash();
        MultiplayerSession.Instance.Rpc(
            nameof(MultiplayerSession.ReceiveChangeEvent), dtoJson, hash);
    }

    EmitSignal(SignalName.ChangeEventApplied, Id);
    EventBus.Emit(EventBus.SignalName.GameChangeEventAfter, ScriptName);
    return true;
}
```

### ReceiveChangeEvent RPC on MultiplayerSession

```csharp
// MultiplayerSession.cs
[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false,
     TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
public async void ReceiveChangeEvent(string dtoJson, string expectedHash)
{
    ChangeEventDto dto = JsonSerializer.Deserialize<ChangeEventDto>(dtoJson);
    ChangeEvent ev     = ChangeEvent.FromDto(dto);
    await ev.ApplyChange(localOnly: true);

    string actualHash = GameSession.Current.GameState.ComputeHash();
    if (actualHash != expectedHash)
    {
        DebugUtilities.PrintPeerError($"Hash mismatch after {dto.GetType().Name}: expected {expectedHash}, got {actualHash}");
        RpcId(1, nameof(RequestResync)); // ask server for full snapshot
    }
}

[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
     TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
public void RequestResync()
{
    if (!Multiplayer.IsServer()) return;
    int requestingPeer = Multiplayer.GetRemoteSenderId();
    string snapshotJson = JsonSerializer.Serialize(GameSession.Current.GameState.BuildSnapshot());
    RpcId(requestingPeer, nameof(ReceiveFullSnapshot), snapshotJson);
}

[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false,
     TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
public void ReceiveFullSnapshot(string snapshotJson)
{
    MultiplayerGameState snapshot = JsonSerializer.Deserialize<MultiplayerGameState>(snapshotJson);
    GameSession.Current.GameState.ApplySnapshot(snapshot);
    DebugUtilities.PrintPeer("Resync complete", DebugVerbosity.INFO);
}
```

### Adding a New ChangeEvent (Checklist)

When adding a new `ChangeEvent` subclass that must be replicated:

1. Create the `ChangeEvent` subclass as normal
2. Create a matching `ChangeEventDto` subclass with only constructor params
3. Register it with `[JsonDerivedType]` on the `ChangeEventDto` base class
4. Implement `ToDto()` on the new `ChangeEvent`
5. Add a `case` to `ChangeEvent.FromDto()` factory switch

---

## Pattern A — Full Snapshot (Secondary / Fallback)

Used in three situations:
- **Session start** — deliver initial state to all clients before play begins
- **Late joiners** — `MultiplayerSynchronizer` delivers `GameStateJson` automatically on connect
- **Resync** — client requests after detecting a hash mismatch (see `RequestResync` above)

### Critical Rule: Always Update In-Place

**Never replace a `StateObject` instance.** All `StateObject` subclasses (`CountryState`, `UnitState`, `CardState`, `StraightState`) extend `GodotObject`. They hold:
- A reference to their Godot scene node (`Node` property — `CountryScene`, `UnitScene`, etc.)
- Signal subscriptions set up in their constructors
- Tag callbacks wired to visual updates

Replacing them with newly deserialized objects breaks all of these. **Always mutate the existing object's fields in `ApplySnapshot()`.**

### MultiplayerGameState DTO types

`MultiplayerGameState` must hold **plain C# POCOs** — no `GodotObject`, no Godot signals, no circular refs — identified by `Id`. The actual `StateObject` types are not directly serializable (required constructors, Node lifecycle, circular refs).

```csharp
// MultiplayerGameState.cs
public partial class MultiplayerGameState
{
    public List<CountryStateDto>  CountryStates  { get; set; } = new();
    public List<UnitStateDto>     UnitStates     { get; set; } = new();
    public List<CardStateDto>     CardStates     { get; set; } = new();
    public List<StraightStateDto> StraightStates { get; set; } = new();
}

public class CountryStateDto
{
    public int Id { get; set; }
    public Dictionary<Faction, int> Units { get; set; } = new();
}

public class UnitStateDto
{
    public int  Id            { get; set; }
    public int  CountryId     { get; set; }
    public bool ImmuneForTurn { get; set; }
}

public class CardStateDto
{
    public int      Id   { get; set; }
    public CardZone Zone { get; set; }
}

public class StraightStateDto
{
    public int Id                   { get; set; }
    public int ControllingCountryId { get; set; }
}
```

Static / immutable data (country names, unit factions, card names) is **not included** — already loaded from data files at startup.

### BuildSnapshot and ApplySnapshot

```csharp
// GameState.cs
public MultiplayerGameState BuildSnapshot()
{
    return new MultiplayerGameState
    {
        CountryStates  = CountryStates.Select(cs => new CountryStateDto  { Id = cs.Id, Units = new(cs.Units) }).ToList(),
        UnitStates     = UnitStates.Select(us    => new UnitStateDto     { Id = us.Id, CountryId = us.CountryId, ImmuneForTurn = us.ImmuneForTurn }).ToList(),
        CardStates     = CardStates.Select(cs    => new CardStateDto     { Id = cs.Id, Zone = cs.Zone }).ToList(),
        StraightStates = StraightStates.Select(ss => new StraightStateDto { Id = ss.Id, ControllingCountryId = ss.ControllingCountryId }).ToList()
    };
}

public void ApplySnapshot(MultiplayerGameState snapshot)
{
    foreach (var dto in snapshot.CountryStates)  { var cs = CountryStateById[dto.Id];  cs.Units = dto.Units; }
    foreach (var dto in snapshot.UnitStates)     { var us = UnitStatesById[dto.Id];    us.CountryId = dto.CountryId; us.ImmuneForTurn = dto.ImmuneForTurn; }
    foreach (var dto in snapshot.CardStates)     { var cs = CardStatesById[dto.Id];    cs.Zone = dto.Zone; }
    foreach (var dto in snapshot.StraightStates) { var ss = StraightStateByControllingCountryId[dto.ControllingCountryId]; ss.ControllingCountryId = dto.ControllingCountryId; }

    // Tags are NOT in the snapshot — always recomputed
    GameStateCalculator.CalculateAll();
}
```

### Adding New State Fields to Snapshots (Checklist)

When a new mutable field needs to travel in the snapshot:

1. **Identify the `StateObject`** that owns the field
2. **Add the field to the corresponding DTO** in `MultiplayerGameState.cs`
3. **Update `BuildSnapshot()`** to populate it
4. **Update `ApplySnapshot()`** to write it back
5. **Update `ComputeHash()`** to include the field

Do **not** add to DTOs: `GodotObject`/scene node references, signal delegates, computed/derived properties, or tag state.

### Initial State / Late Joiners

The `MultiplayerSynchronizer` in `MultiplayerSession.tscn` syncs the `GameStateJson` string property when a new peer connects. Keep it current on the server:

```csharp
// MultiplayerSession.cs — HandleGameStateChange()
GameStateJson = JsonSerializer.Serialize(GameSession.Current.GameState.BuildSnapshot());
```

Clients receiving `GameStateJson` via the synchronizer call `ApplySnapshot` in `HandleGameStateJsonChange()`.

---

## Key Files

| File | Role |
|------|------|
| `scenes/network/MultiplayerSession.cs` | `ReceiveChangeEvent`, `RequestResync`, `ReceiveFullSnapshot`, `ReceiveGameStateUpdate` RPCs |
| `scenes/network/MultiplayerGameState.cs` | Snapshot DTO container + individual DTO classes |
| `scripts/C#/ChangeEvents/ChangeEvent.cs` | `ApplyChange(localOnly)`, abstract `ToDto()`, static `FromDto()` |
| `scripts/C#/ChangeEvents/ChangeEventDto.cs` | DTO hierarchy with `[JsonPolymorphic]` registration |
| `scripts/C#/Game/GameState.cs` | `BuildSnapshot()`, `ApplySnapshot()`, `ComputeHash()` |
| `scripts/C#/DataObjects/GameStateCalculator.cs` | Called after every `ApplyChange` and `ApplySnapshot` |

---

## Debugging State Divergence

1. **Hash mismatch logged?** — the `DebugUtilities.PrintPeerError` in `ReceiveChangeEvent` tells you which event caused the split
2. **Missing DTO field?** — a mutable field not in the DTO is silently not synced; check `BuildSnapshot` covers every field in `ComputeHash`
3. **`FromDto` producing wrong params?** — add logging inside `FromDto` to print reconstructed values vs expected
4. **`GameStateCalculator.CalculateAll()` missing?** — tags (`InSupply`, `Attackable`, etc.) won't update; must run after every `ApplyChange` and `ApplySnapshot`
5. **Tags are never hashed or snapshotted** — they are always fully recomputed, never sent over the wire
