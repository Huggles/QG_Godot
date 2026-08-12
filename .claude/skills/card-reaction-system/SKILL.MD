---
description: Documentation for the card reaction system, including how play steps and reactions work, the flow of events, and how to implement cards with multiple steps and reactions.
---

> **Authority:** This skill describes the *implementation* of the reaction system. If anything here conflicts with the board game rules in `reaction-specifics`, **`reaction-specifics` takes precedence** and the implementation should be updated to match.

# Card Reaction System Documentation

## Overview

The card reaction system allows factions to respond to actions taken during the play step. This creates a dynamic, interactive gameplay where each action can trigger a chain of responses and counter-responses.

## Core Concepts

### 1. Card Steps

Each card can have multiple steps:
- **Play Steps**: Executed when a card is played from hand
- **React Steps**: Executed when a reaction card is activated

Steps execute sequentially, and each step can trigger reactions from other players.

### 2. Reaction Timing

There are two types of reactions based on timing:

#### Block Reactions (Before)
- Triggered BEFORE a change event is applied
- Can prevent the change from happening by blocking it
- Identified by having an `IsBlockRequest` condition in their `CardTriggers()`
- Example: Canceling an attack before it happens

#### After Reactions (After)
- Triggered AFTER a change event is applied
- Respond to the results of an action
- Any reaction WITHOUT `IsBlockRequest` is an after-reaction
- Example: Deploying units after an enemy attacks

### 3. Faction Order

Reactions are requested in a specific order:
- If the last action was by an **Axis** faction → **Allies go first** (UK, Soviet, US, then Germany, Japan, Italy)
- If the last action was by an **Allied** faction → **Axis go first** (Germany, Japan, Italy, then UK, Soviet, US)
- This ensures the opposing team always gets priority for reactions
- Factions can respond to their own team's actions (but not block their own)

## Reaction Chain Flow

### Initial Card Play

When a faction plays a card during the play step:

1. **PlayCardChangeEvent** is created and processed
   - Block reactions requested from all factions in order
   - If blocked, the card is not played
   - If not blocked, the card is moved from hand to discard/play area

2. **First Card Step** is executed
   - The step's logic runs (e.g., deploy unit, attack country)
   - Creates a result ChangeEvent (e.g., `DeployUnitChangeEvent`, `BattleCountryChangeEvent`)

3. **Result ChangeEvent** is processed
   - Block reactions requested (other factions can block this step)
   - If not blocked, the change is applied
   - After reactions requested

### Recursive Response Chain

When a faction plays a reaction (either block or after):

1. The reaction goes through the **same flow** as the original card
   - Creates introduction event (`ActivateReactionChangeEvent`)
   - Can be blocked by other factions
   - Executes the reaction's steps
   - Each step can trigger more reactions

2. This creates a **recursive chain**:
   ```
   Card A played
     → Block reaction from Player B
       → Block reaction to B from Player C
         → After reaction to C from Player D
           → (continues until no one responds)
       → If B blocked A, A stops here
     → If not blocked, A's step executes
     → After reactions requested
       → After reaction from Player E
         → (recursive chain continues)
       → (continues until no one responds)
   ```

3. **Response Priority Rules**:
   - Each time a response is played, faction order is recalculated based on who triggered it
   - Factions can respond to their own team's actions
   - But cannot block their own specific actions

### Multi-Step Cards

Cards can have multiple steps (both play steps and react steps):

1. After a step completes and **all reactions to that step are finished**, the system checks for the next step

2. If the card has more executable steps:
   - The next step is automatically executed
   - Goes through the same reaction flow
   - Continues until all steps are complete

3. **React steps on reactions**:
   - Reaction cards can also have multiple steps
   - These steps can be triggered during any reaction cycle
   - Each reaction can use its steps only once per turn

## Code Structure

### Key Methods

#### `DoActivationOption(CardActivationOption)`
Top-level method that processes a card being played or activated:
1. Creates introduction event (PlayCard or ActivateReaction)
2. Processes that event (blocks, apply, reactions)
3. Executes the card step
4. Processes the step result

#### `DoChangeEvent(ChangeEvent)`
Central method that processes any change event:
1. Adds event to pool
2. Requests block reactions
3. Applies change if not blocked
4. Requests after reactions (for non-introduction events)

#### `RequestBlockReactions(ChangeEvent)`
Requests block reactions from all factions in order:
- Skips the faction that triggered the event
- Each faction can play one block reaction
- Block reactions are processed recursively

#### `RequestAfterReactions(ChangeEvent triggerEvent)` → returns `bool`
Requests after reactions from all factions. Returns `true` if at least one reaction was played, `false` if everyone skipped or had no options.

- Takes the `triggerEvent` that opened this reaction window
- Sets `CurrentReactionTrigger = triggerEvent` for the duration of the window, then restores the previous value in a `finally` block (safe for nested windows)
- Iterates factions in `RequestOrder`; skips factions in `_afterReactionPassedFactions`
- If a faction skips → added to `_afterReactionPassedFactions` (not asked again until a new reaction is played)
- If a faction plays → `_afterReactionPassedFactions.Clear()` **before** `DoCard`, so all factions get a fresh chance during that reaction's resolution; then `break` to restart with updated `RequestOrder`
- Loop exits when a full pass through all non-skipped factions produces no plays

#### `CurrentReactionTrigger` (property on `CardPlayRound` / `CardPlayPool`)
The change event that opened the currently-active reaction window. Card step logic reads this to determine *which specific event* it is reacting to, so targets are computed relative to that event rather than all matching events in the pool.

- Set at the start of each `RequestAfterReactions` call, restored (stack-safe) in the `finally` block
- Accessible from card logic via `CardPlayPool.CurrentReactionTrigger`
- Nested windows work correctly: the inner window overwrites the value, the outer window gets it back when the inner window finishes
- During `ContinueWithNextSteps`, the trigger is the restored outer value — subsequent steps that rely on `SourceCardId` (not `CurrentReactionTrigger`) are unaffected

#### `ActivationTrigger` (property on `CardLogic`)
The change event **this card's activation is reacting to** — the event offered for block, or the event that opened the after-reaction window. This is what card *step logic* should read.

- `DoCard(int cardId, ChangeEvent activationTrigger = null)` binds it before the introduction event, and releases it once every step of the card is finished (so a card resumed by `ContinueWithNextSteps` keeps the event it originally reacted to)
- Passed explicitly by `RequestBlockReactions` (the event being blocked) and `RequestAfterReactions` (the window's trigger). Not inferred from `CurrentBlockTrigger`/`CurrentReactionTrigger`: inside an after-reaction window nested in a block window both are set
- Null for a card played from hand outside a reaction, and never bound for a Status/Response card being played *onto the table* — that is not an activation
- Save/restore is stack-safe for nested activations of the same `CardLogic`; `OnNewTurnStarted` and `ClearPool()` clear it as a safety net

**Prefer this over `CardPlayPool.LastNoneNewCardChangeEvent` in step logic.** That property is a live pool read, and *every* `ChangeEvent.Apply()` self-registers into `ChangeEventsPool` (see `ChangeEvent.ApplyMutation`). So a prerequisite event applied inside the step — or a reaction played in the activation window `DoCard` opens before the steps run — silently retargets it at a different event. This is what made `StatusRadar` discard its 2 cards and then fail to block the Navy removal.

`CardTriggers()` cannot use this — trigger conditions are evaluated *before* activation, when nothing is bound yet. Block triggers use `CurrentBlockTrigger` instead; see below.

#### `CurrentBlockTrigger` (property on `CardPlayRound` / `CardPlayPool`)
The change event currently being **offered for block**, or null when no block window is open. This is what a **block reaction's `CardTriggers()`** must match against.

- Set at the start of `RequestBlockReactions`, restored (stack-safe) in the `finally` — a block card's own introduction event opens a nested window, and the outer window's remaining factions need their trigger identity back
- `RequestBlockReactions` recalculates tags *inside* its loop, after `CurrentBlockTrigger` is set and again after any block card resolves, so conditions are always evaluated against the open window
- Also drives the block prompt's trigger label and the "cannot block your own action" guard in `RequestBlock`

**Never match a block trigger against `CardPlayPool.LastNoneNewCardChangeEvent`.** That property deliberately skips `PlayCardChangeEvent` and `ActivateReactionChangeEvent`, so inside the block window opened for a card play or a reaction activation it reports the *earlier* event. That is what made every removal-blocker (e.g. UK's Destroyers) offer itself against StatusRadar being activated — a card activation is not a unit removal, and no card in the game blocks a card play at all.

So the rule is symmetric: **step logic reads `ActivationTrigger`, block trigger conditions read `CurrentBlockTrigger`** — neither re-reads the live pool.

One consequence to expect: a block card only claims `Tag.IsActivatable` while its block window is open. That is correct — block cards are offered solely through `GetBlockReactionOptions` inside `RequestBlock`.

#### `ContinueWithNextSteps()`
Checks all cards in the pool for executable next steps:
- **Only called if `RequestAfterReactions` returned `true`** — if everyone skipped/had no options, the pipeline stops immediately and the round ends
- Executes steps sequentially; each step triggers the full reaction chain
- When no more steps available, play step ends

#### `_afterReactionPassedFactions` (instance field, `HashSet<Faction>`)
Tracks which factions have explicitly passed in the current reaction chain:
- Shared across all nested `RequestAfterReactions` calls within the same round
- Cleared when a new reaction is about to be played (before `DoCard`)
- Cleared by `ClearPool()` when the round ends
- Prevents a faction from being asked twice for the same reaction window

### Helper Methods

- `GetNextActions(Faction)`: Returns all available actions for a faction (play from hand or activate reactions)
- `GetAfterReactionOptions(Faction)`: Returns only after-reactions (filters out block reactions)
- `BlockChangeEvents(Faction)`: Returns only block reactions for a faction
- `ActivatableReactions(Faction)`: Returns all activatable reactions including continued steps

## Example Scenario

### Scenario: Germany Builds Army, UK Responds

1. **Germany plays "Build Army" card**
   ```
   PlayCardChangeEvent → UK/Soviet/US check for blocks → None block → Play approved
   First step executes: Select country and create DeployUnitChangeEvent
   ```

2. **DeployUnitChangeEvent triggered**
   ```
   UK/Soviet/US check for blocks → None block → Army deployed
   After reactions requested:
     - UK plays "Air Raid" response (destroys the deployed army)
   ```

3. **UK's "Air Raid" activates**
   ```
   ActivateReactionChangeEvent → Germany/Japan/Italy check for blocks → None block → Activation approved
   Air Raid step executes: Create RemoveUnitChangeEvent
   ```

4. **RemoveUnitChangeEvent triggered**
   ```
   Germany/Japan/Italy check for blocks
     - Germany plays "Anti-Aircraft" (blocks the removal)
   ```

5. **Germany's "Anti-Aircraft" activates**
   ```
   ActivateReactionChangeEvent → UK/Soviet/US check for blocks → None block → Activation approved
   Anti-Aircraft step executes: Blocks UK's RemoveUnitChangeEvent
   ```

6. **Resolution**
   ```
   Germany's Anti-Aircraft blocked UK's Air Raid
   Army remains deployed
   After reactions requested → None play → Continue
   Build Army card has no second step → Play step complete
   ```

## Implementing Cards

### Single-Step Action Card

```csharp
public override List<CardStep> InitializePlayCardSteps()
{
    return new List<CardStep> {
        new CardStep(this, async() => {
            int selectedCountryId = await new SelectCountryHandler(targetCountries).Handle();
            DeployUnitChangeEvent deployEvent = BuildChangeEvent(new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.BUILD));
            deployEvent.IsTrigger = true;  // Allows reactions
            return deployEvent;
        })
        .WithCondition(() => Condition.Build(new Condition.HasBuildableLand(Faction), this))
        .WithGuidance("Build an army")
    };
}
```

### Multi-Step Action Card

```csharp
public override List<CardStep> InitializePlayCardSteps()
{
    return new List<CardStep> {
        new CardStep(this, async() => {
            // First step: Attack
            BattleTarget target = await new SelectBattleTargetHandler(targets).Handle();
            return BuildChangeEvent(target.ToAttackChangeEvent(Faction));
        })
        .WithGuidance("Attack a country"),
        
        new CardStep(this, async() => {
            // Second step: Deploy (happens after attack and all reactions)
            int countryId = await new SelectCountryHandler(countries).Handle();
            return BuildChangeEvent(new DeployUnitChangeEvent(Faction, countryId, DeployType.BUILD));
        })
        .WithGuidance("Build after attacking")
    };
}
```

### Block Reaction Card

`IsBlockRequest` is only a marker — its `MeetCondition()` returns an unconditional `true`. The *second* condition is what decides which event this card can block, and it must test `CurrentBlockTrigger`. Without that test the card is offered against every event that opens a block window, including card plays and reaction activations.

```csharp
protected override List<Condition> CardTriggers()
{
    return new List<Condition> {
        Condition.Build(new Condition.IsBlockRequest(), this),  // Marks as block reaction
        // Scoped to the offered event. UnitAboutToBeRemoved reads CurrentBlockTrigger internally;
        // a hand-rolled CustomCondition must do the same.
        Condition.Build(new Condition.UnitAboutToBeRemoved(Faction.UNITED_KINGDOM, UnitType.NAVY), this)
    };
}

public override List<CardStep> InitializeReactCardSteps()
{
    return new List<CardStep> {
        new CardStep(this, async() => {
            // Block by marking the event THIS activation was offered to block. Read it from
            // ActivationTrigger, never from CardPlayPool.LastChangeEvent /
            // LastNoneNewCardChangeEvent — those are live pool reads that any intervening
            // Apply() retargets. Always type-check: it is null for a non-reaction play.
            if (ActivationTrigger is not BattleCountryChangeEvent battleEvent) return null;

            battleEvent.IsBlocked = true;
            await Task.CompletedTask;
            return null;  // No follow-up change event
        })
        .WithGuidance("Cancel enemy attack")
    };
}
```

### After Reaction Card

```csharp
protected override List<Condition> CardTriggers()
{
    return new List<Condition> {
        // No IsBlockRequest - this is an after-reaction
        Condition.Build(new Condition.FactionBattled(Faction), this)
    };
}

public override List<CardStep> InitializeReactCardSteps()
{
    return new List<CardStep> {
        new CardStep(this, async() => {
            // First react step: Counter-attack
            BattleTarget target = await new SelectBattleTargetHandler(targets).Handle();
            return BuildChangeEvent(target.ToAttackChangeEvent(Faction));
        })
        .WithGuidance("Counter-attack"),
        
        new CardStep(this, async() => {
            // Second react step: Reinforce (can be triggered later in the reaction chain)
            int countryId = await new SelectCountryHandler(countries).Handle();
            return BuildChangeEvent(new DeployUnitChangeEvent(Faction, countryId, DeployType.RECRUIT));
        })
        .WithGuidance("Recruit reinforcements")
    };
}
```

### Reaction-Scoped Targeting with `CurrentReactionTrigger`

When a card can trigger in multiple different reaction windows (e.g. reacting to any sea battle in the pool), the targets must be computed from the *specific event that opened the current window*, not from all matching events.

Use `CardPlayPool.CurrentReactionTrigger` for both `CardTriggers()` and step logic:

```csharp
// CardTriggers: only available in a window opened by the relevant event type
protected override List<Condition> CardTriggers()
{
    return new List<Condition> {
        Condition.Build(new Condition.CustomCondition(() => {
            // DT is only available immediately in the reaction window of a Japan sea battle
            return CardPlayPool.CurrentReactionTrigger is BattleCountryChangeEvent ce
                && ce.TriggeringFaction == Faction
                && ce.CountryState.Type == CountryType.SEA;
        }), this)
    };
}

// Step logic: targets derived from the specific triggering event
new CardStep(this, async() => {
    var triggerBattle = CardPlayPool.CurrentReactionTrigger as BattleCountryChangeEvent;
    if (triggerBattle == null) return null;
    var adjacentBuildable = CountryState.BuildableLand(Faction)
        .Where(cs => triggerBattle.CountryState.ConnectedCountryStates.Contains(cs))
        .ToList();
    int selectedCountryId = await new SelectCountryHandler(adjacentBuildable.ToCountryIds()).Handle();
    return BuildChangeEvent(new DeployUnitChangeEvent(Faction, selectedCountryId, DeployType.BUILD));
})
```

**Why this matters:** If two sea battles happened (e.g. Bay of Bengal, then South China Sea via Surprise Attack), `CurrentReactionTrigger` tells the card *which* battle opened the window it's being played in, so targets are correctly scoped to that battle's sea space — not all sea spaces in the pool.

**`CardTriggers()` vs pool-based conditions:**
- Use `CurrentReactionTrigger` in `CardTriggers()` for response cards that react *immediately* to a specific event — ensures the card only appears as an option in the correct window
- Use pool-based conditions (`HasBattledAtSea`, etc.) for status cards that can score or activate at any point after the event occurred

### Prerequisite Change Events Within a Step

Sometimes a card step must apply a mandatory side-effect (e.g. discard cards, pay a cost) before returning its main change event. Because the step is `async`, you can apply that prerequisite inline using `Apply()` directly — no need to split into two steps or go through `CardPlayPool.DoChangeEvent`.

> **`Apply()` always registers the event into the round's `ChangeEventsPool`** (`ChangeEvent.ApplyMutation`). `IsTrigger = false` suppresses the block/reaction chain, *not* the registration. So after a prerequisite `Apply()`, `CardPlayPool.LastNoneNewCardChangeEvent` and `LastChangeEvent` point at the prerequisite. Resolve anything you need from `ActivationTrigger` (or capture it into a local) **before** applying the prerequisite.

```csharp
new CardStep(this, async() => {
    // Prerequisite: discard 2 cards and sync to clients
    ForceDiscardCardsChangeEvent discardEvent = new ForceDiscardCardsChangeEvent(Faction, Faction, 2);
    discardEvent.IsTrigger = false;   // not blockable, no reactions
    await discardEvent.ApplyChange(); // executes, broadcasts, and populates DiscardedCardIds

    // Use runtime results from the prerequisite if needed
    List<PresentationItem> items = PresentationItemCard.FromCardIds(discardEvent.DiscardedCardIds, false);
    await PresentationModal.Current.ShowModal(items, "Discarded cards");

    // Main action
    int countryId = await new SelectCountryHandler(targets.ToCountryIds()).Handle();
    return BuildChangeEvent(new DeployUnitChangeEvent(Faction, countryId, DeployType.BUILD));
})
```

**Rules for prerequisite change events:**
- Set `IsTrigger = false` so no block/reaction chain is triggered
- Call `Apply()` directly (not `CardPlayPool.DoChangeEvent`) — no block/after-reaction chain is opened
- **Resolve `ActivationTrigger` (or any pool read) into a local BEFORE the `Apply()`** — `Apply()` registers the event into `ChangeEventsPool` and moves `LastNoneNewCardChangeEvent`/`LastChangeEvent` onto it
- Any runtime results (e.g. `DiscardedCardIds`) are available on the local instance immediately after `await`

## Key Design Principles

1. **Recursive by Nature**: Every reaction goes through the same flow as the original action, enabling deep chains
2. **Faction Order Matters**: Opponents always get first chance to respond
3. **Steps Execute Sequentially**: Next steps only execute after all reactions to previous steps complete, AND only if at least one reaction was played
4. **Two Reaction Timings**: Block (before) and After (after) reactions provide strategic options
5. **Self-Response Allowed**: Factions can respond to their own team's actions, but not block their own
6. **Pass-Once Per Window**: Once a faction skips a reaction, they aren't asked again until a new reaction resets the window
7. **Priority Reset on Play**: When any faction plays a reaction, all pass-locks are cleared before resolving it — everyone gets a fresh opportunity during its resolution
8. **Round Ends on All-Skip**: If no faction plays a reaction (everyone skips or has no options), `ContinueWithNextSteps` is skipped and `Finish()` is called at the top-level `DoCard`
9. **`Finish()` is Unconditional at Top Level**: `DoCard` calls `Finish()` whenever `reactionDepth` returns to 0 for the initial play — the old `hasRemainingActions` guard was wrong because skipped reactions don't consume their tags
10. **Reaction Windows are Trigger-Scoped**: `CurrentReactionTrigger` identifies which specific event opened the active window. Cards that can react to multiple different events (e.g. a card reacting to *any* sea battle in the pool) must use `CurrentReactionTrigger` to scope their targets to the correct event — not scan all matching events in the pool
11. **Activations Bind Their Trigger**: `DoCard` pins the triggering event onto the `CardLogic` as `ActivationTrigger` for the whole activation. Step logic reads that, not the pool — a live pool read is retargeted by any `Apply()` that lands in between

## Common Pitfalls

1. **Forgetting `IsTrigger = true`**: ChangeEvents must have this flag to allow reactions
2. **Not marking block reactions**: Block reactions need `IsBlockRequest` in their triggers
3. **Infinite loops**: Ensure reaction triggers have conditions that eventually become false
4. **Blocking yourself**: The system prevents self-blocking, but be aware of the rules
5. **Step prerequisites**: Make sure prerequisite steps are properly set for multi-step cards
6. **Double-ask bug**: `RequestAfterReactions` is called at every `DoChangeEvent` level. Without `_afterReactionPassedFactions`, a faction could be asked inside a nested reaction window AND again in the outer loop restart. Always use the shared pass-tracking set.
7. **Stale `hasRemainingActions` check**: Checking for remaining reaction tags to decide whether to `Finish()` is incorrect — a skipped faction still holds the tag. `Finish()` must always fire when returning to `reactionDepth == 0`.
8. **Matching a block trigger against the pool**: a block card's `CardTriggers()` must test `CardPlayPool.CurrentBlockTrigger`. Testing `LastNoneNewCardChangeEvent` matches inside *every* block window — including the one opened for a card play or reaction activation, which that property filters out — so the card gets offered against events it has nothing to do with.
9. **Reading the pool in step logic**: `CardPlayPool.LastNoneNewCardChangeEvent` / `LastChangeEvent` are live reads. Between the card being chosen and its step running, the introduction event's own reaction window and any prerequisite `Apply()` both push onto the pool, so the read silently lands on the wrong event and the `is SomeChangeEvent` pattern fails with no error. Use `ActivationTrigger` in step logic; keep the pool read only in `CardTriggers()`, which runs before activation.
