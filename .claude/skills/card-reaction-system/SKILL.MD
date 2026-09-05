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

### 3. Team Turns

A reaction window is a sequence of **team turns**, not a walk down a list of factions. The rules
(`reaction-specifics`) define an order between the two *teams* and none at all between the factions
inside one:

- The team that did **not** cause the trigger takes the first turn. If nothing caused it — a game
  mechanic rather than a country's action — the team of the faction taking its turn goes second.
- A team uses **one** reaction per turn. Then the turn passes to the other team.
- A team may use several reactions to the same trigger, in whatever order it likes, as long as the
  other side gets a turn in between.
- Two consecutive team turns with nothing played close the window.
- Factions can respond to their own team's actions (but not block their own).

`RequestAfterReactions` implements this with a **turn pointer** — a local, seeded from the event that
opened the window and flipped after every turn, played or passed. See its section below.

Within a turn, every eligible faction of the team is prompted **at the same time** and the first card
chosen wins; the rest are withdrawn. That race *is* the team's choice of which reaction to use, since
the rules give no order to impose. The flat six-faction `RequestOrder` this replaced invented one
(UK always before Soviet before US) and let a team react twice in a row, because after a play it
restarted from the top of the same team's slice.

## Reaction Chain Flow

### Initial Card Play

When a faction plays a card during the play step:

1. **PlayCardChangeEvent** is created and processed
   - The card is moved from hand to discard/play area
   - **No window opens at all** — an introduction event is neither blockable nor reactable, see below

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
   - Creates introduction event (`ActivateReactionChangeEvent`) — opens no window of its own
   - Executes the reaction's steps
   - Each step can trigger more reactions

2. This creates a **recursive chain**:
   ```
   Card A played  (the play itself opens no window)
     → A's step executes, producing a change event
       → Block reaction from Player C
         → Block reaction to C from Player D
           → (continues until no one responds)
         → If C blocked the change event, it is not applied
       → If not blocked, the change is applied
       → After reactions requested
         → After reaction from Player E
           → (recursive chain continues)
         → (continues until no one responds)
   ```

3. **Response Priority Rules**:
   - Each window has its own turn pointer, fixed by the event that opened it. Playing a reaction does
     not recalculate it — the nested window the reaction opens gets its own, and the outer window
     simply passes the turn to the other team
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
2. Applies that event (no window of any kind opens on it)
3. Executes the card step
4. Processes the step result

#### `DoChangeEvent(ChangeEvent)`
Central method that processes any change event:
1. Adds event to pool
2. Requests block reactions
3. Applies change if not blocked
4. Requests after reactions (for non-introduction events)

#### Introduction events open no window at all
`DoCard` builds both introduction events — `PlayCardChangeEvent` and `ActivateReactionChangeEvent` — with **`IsTrigger = false`**, applies them, and goes straight on to the card's steps. Neither a block window nor an after-reaction window opens on them:

```csharp
ChangeEvent introEvent = !cardState.IsPlayed
    ? new PlayCardChangeEvent(cardState.Id)
    : new ActivateReactionChangeEvent(cardState.Faction, cardState.Id, activationTrigger);
introEvent.IsTrigger = false;
await introEvent.Apply();
// ... straight to the step loop
```

**Nothing in the game blocks a card play or a reaction activation.** Every block card is scoped to the *event a step produced*, never to the declaration: `RemoveUnitChangeEvent` (via `Condition.UnitAboutToBeRemoved`: Radar, BattleshipRepair, Leningrad, Moscow, Stalingrad, Destroyers, RAF, DefensivePosture, MonteCassino, KwantungArmy), `ForceDiscardCardsChangeEvent` (ASWTactics, SkilledPilots), or any event a given card produced (via `Condition.IsBlockRequestFromCard`: EnigmaCodeCracked). Per `reaction-specifics` you react to a reaction's *effect* — Destroyers blocking the removal Surprise Attack causes — never to its activation.

The window could not have worked even if a card matched it. `Apply()` ran **before** the block request, the inverse of `DoChangeEvent`: by the time anyone was asked, the card was already on the table, `CardsPlayedThisTurnStep` already incremented and the "X plays Y" modal already enqueued, so a block could only suppress the card's *steps* — never un-play the card. Suppressing the steps is what a block on the first step's event does anyway, and it does it without a window on every play in the game.

The cost of keeping it was real: combined with the always-ask rule, every card play and every activation fired a blocking `SendInputRequest` at every faction holding a face-down Response card, each preceded by a `GameStateCalculator.CalculateAll()`.

**The after-reaction window on an introduction event — the old "activation window" — is gone for the same reason.** It ran on every single card play and every activation, cost two `GameStateCalculator.CalculateAll()` passes (each broadcasting a `RecalculateTagsMessage` to every peer) plus a blocking prompt, and existed to serve two cards. Reacting to a card being played or activated is now expressed through windows the card's own resolution already opens — see **Reacting to a play or an activation** below.

#### `TakeTeamTurn(candidates, ask, passed)` → `(Faction, int cardId)`
One team's turn, shared by both windows. Prompts every candidate faction at once and returns the
first card any of them chooses.

- Candidates are grouped by controlling peer (`PlayerFactionRegistry.GetPeerIdForFaction`), the same
  shape `OpeningDiscard` uses. Groups run concurrently; the factions inside a group run in sequence,
  because a peer is only ever asked one thing at a time. **A single-process game — the CLI, a hotseat — collapses
  to one group and stays fully sequential**, which is why `.qgc` scripts still drive one prompt at a time.
- The first card chosen cancels the turn's `CancellationTokenSource`. Every still-open prompt is
  withdrawn through `NetworkApi.SendInputRequest`'s `withdrawToken`, which clears the host's awaiter
  and then `AbortRemoteInput`s that one peer.
- A faction that **declined** is added to `passed`. A faction that was **withdrawn**, or never reached
  because the turn was already decided, is not — it made no decision and must be offered again on the
  team's next turn. A card chosen a hair too late is discarded and logged, for the same reason.
- `Task.WhenAll` before returning: the caller must not resolve the winning card while another peer's
  prompt is still live, or `DoCard` mutates state alongside it and the peers' GameMessage ids and
  state hashes stop agreeing.

#### `RequestBlockReactions(ChangeEvent)`
Only the **opponent team** is offered a block, and it takes repeated turns until it produces no play
or the event is blocked. Every block card in the game reacts to an opponent's `RemoveUnitChangeEvent`
or `ForceDiscardCardsChangeEvent`, so a turn for the acting team would be an empty window in every
case that exists.

- Computes the team itself from `changeEvent.TriggeringFaction` — the caller passes no order
- Skips the faction that triggered the event
- `GameStateCalculator.CalculateAll()` once per turn rather than once per faction: a turn raises all
  of its prompts from one tag snapshot, so the old `tagsDirty` bookkeeping is gone and this is
  strictly fewer calls
- Reached only from `DoChangeEvent` in practice; the `ProcessIntroductionEvent` call site is gated off by `IsTrigger = false`

#### `RequestAfterReactions(ChangeEvent triggerEvent)` → returns `bool`
Runs the window as alternating team turns. Returns `true` if at least one reaction was played, `false` if everyone skipped or had no options.

- Takes the `triggerEvent` that opened this reaction window
- Sets `CurrentReactionTrigger = triggerEvent` for the duration of the window, then restores the previous value in a `finally` block (safe for nested windows)
- **The turn pointer is a local**, seeded by `FirstTeamToReactTo(triggerEvent)` and flipped at the end
  of every turn whether or not anything was played. A local is what makes it per-window: a reaction
  recurses into `DoCard`, which opens its own windows with their own pointers, and when that unwinds
  this frame resumes on the team whose turn it actually was. A field would be clobbered by the nested
  window, and the old `RequestOrder` — a live read of `LastChangeEvent` — was clobbered by the nested
  window's change events.
- Candidates for a turn are that team's factions, minus `_afterReactionPassedFactions`, minus
  anything `ShouldOpenReactionWindow` declines
- If a faction skips → added to `_afterReactionPassedFactions` (not asked again until a new reaction is played)
- If a faction plays → `_afterReactionPassedFactions.Clear()` **before** `DoCard`, so all factions get
  a fresh chance during that reaction's resolution — but the turn still passes to the other team, so
  the team that just played cannot play again until the opponent has had its chance
- Loop exits after two consecutive team turns with no play
- **Reached only from `DoChangeEvent`.** An introduction event never opens an after-reaction window,
  so `triggerEvent` is always a real change event. There is consequently no faction filter and no
  "exclude the card being introduced" special case — neither situation can arise. A card cannot react
  to its own introduction because no introduction opens a window.

#### `ShouldOpenReactionWindow(Faction, List<int> options)` — the always-ask rule
The single gate deciding whether a faction is prompted at all. Used by **both** `RequestAfterReactions` and `RequestBlock`, so the rule lives in one place.

```csharp
if (GameFlow.Instance.IsSkippingReactions(faction))
    return options.Any(IsPubliclyVisibleTableCard);
return options.Count > 0 || HasHiddenResponseCards(faction);
```

**Why a faction with nothing to play is still asked.** Opening a window only when a reaction is actually available makes the *appearance* of the prompt proof that the faction holds a face-down Response card matching exactly that event — and its absence proof that it holds none. Every opponent could read the hidden card straight off "Waiting on Japan input…". So any faction holding an unrevealed Response card (`HasHiddenResponseCards`) is asked in every window and passes with Skip. The empty prompt is the cover story.

Only unrevealed **Response** cards earn this. Status cards sit face up and their trigger conditions are computable by every player from public state, so a Status-driven window hides nothing.

**Scoped skips.** Always-asking produces a lot of empty prompts, so a reaction prompt also offers *Skip rest of turn step* and *Skip rest of round* (`ReactionSkipScope`). The chosen scope rides back on `InputRequest.ReactionSkipScope` and is recorded by `GameFlow.RecordReactionSkip`.

A third scope, `ReactionSkipScope.UNTIL_ACTIVATABLE`, means *only ask me when something is genuinely activatable* — a standing preference with no expiry. **It is enforced on the client and `GameFlow.RecordReactionSkip` deliberately ignores it.** Honouring it host-side in `ShouldOpenReactionWindow` would be cheaper and would undo the always-ask rule for that faction: with the empty windows gone, a window opening would once again be proof that the faction holds a face-down Response card matching exactly that event. Instead the host opens every window as before and `InputRequest.TryAutoPassArmedReactionWindow` answers the empty ones on the controlling peer, so every "Waiting on X input…" line the other players see stays exactly where it was.

All four scopes can be pre-armed before any prompt arrives, from the toggle in each local faction's `FactionInfoRow`. `ReactionSkipPreference` is the client-local, per-faction holder behind both that toggle and the in-prompt buttons; it mirrors `GameFlow`'s expiry rules (including `DropOwnTurnReactionSkip`) off replicated `GameFlow` state, so a client's answer cannot drift from the host's.

A scoped skip silences **only** the windows that exist to hide information. `IsPubliclyVisibleTableCard` (a Status card, or a Response card an earlier activation already revealed) keeps its window: everyone can see you hold it, so that prompt leaks nothing whether it appears or not, and the player must not lose a real reaction to a convenience button. Once the window is open the faction is offered *all* its activatable options, hidden ones included — the prompt is already explained by the public card.

`GameFlow`'s two registries are host-only and self-expiring: each records the turn/step or round it was set in and stops being honoured once the game moves past it, so there is no clearing hook to keep in step with the turn loop. `ChangeRoundChangeEvent` calls `DropOwnTurnReactionSkip()` so a round-scope skip never survives into its owner's own turn — reacting to your own battle (Destroyer Transport, Surprise Attack) is core play and must not be muted by a decision taken on somebody else's turn.

**What the prompt shows.** A reaction window draws the faction's *whole* event-triggered table — `ReactionWindowDisplayCardIds`, carried as `InputRequest.DisplayCardIds` — and greys out everything not in `TargetCardIds`. Showing only the playable cards left the player guessing why a card they knew they held was absent, and an always-ask window showed an empty table, which reads as a bug rather than as "nothing of yours triggers here". The two piles are filtered differently. **Status** cards must have an event-based trigger (`HasEventBasedTrigger`), so a passive modifier — or a scoring card, or a Play-step activation — is not shown as a reaction that merely is not available. **Response** cards are shown unconditionally: a Response card is on the table precisely to be activated, and an unrevealed one is what opened the window in the first place, so filtering it out produced the exact empty prompt this list exists to explain. That is what kept the three purely state-based Response cards — Defense of the Motherland, Mobile Force, Truk (play step / own turn) — out of every reaction window while causing one on every event.

`ReactionWindowDisplayCardIds` also takes the window's `TargetCardIds` and folds them in, so the display list is **always a superset of the offered set**. `FactionHandDisplay` draws only the display list, so an offered card missing from it is unclickable — reachable because `ReactionDepth` is incremented only in `DoCard`, so a trigger event raised outside a card play (a step mutator, the draw step) opens an after-reaction window at depth 0, where `CanBeActivated`'s `reactionDepth == 0` branch admits a purely state-based Status card that the Status filter above excludes.

Every card the window actually offers also gets the **foil-sweep emphasis** (`CardScene.SetEmphasized`, driven by `ActiveCardPrompt.IsReactionWindow` through `FactionHandDisplay.LayoutFan`). Because the always-ask rule makes most reaction prompts pure cover with nothing playable, the rare window that does offer something must not look like the rest — and the greyed-out majority is already explained by the availability scrim. It is gated on the same selectability as the click, so an emphasised card is always one the host will accept.

Clickability comes from `TargetCardIds`, never from `Tag.IsActivatable`: block-eligible cards carry `Tag.IsBlockReaction`, which is server-internal and never replicated, so a client re-deriving the answer would grey out a card the host is actually offering.

**Consequence for tests.** "Was a prompt raised" no longer proves a card matched. A test that detected a wrongly-offered reaction by hanging on an unexpected prompt must assert on the option count instead — `assert promptoptions == 0` in the CLI. See `tests/qgc/block_reaction_prevents_removal.qgc` and `tests/qgc/reaction_always_asked_and_scoped_skip.qgc`.

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

**Prefer this over `CardPlayPool.LastNoneNewCardChangeEvent` in step logic.** That property is a live pool read, and *every* `ChangeEvent.Apply()` self-registers into `ChangeEventsPool` (see `ChangeEvent.ApplyMutation`). So a prerequisite event applied inside the step silently retargets it at a different event. This is what made `StatusRadar` discard its 2 cards and then fail to block the Navy removal.

`CardTriggers()` cannot use this — trigger conditions are evaluated *before* activation, when nothing is bound yet. Block triggers use `CurrentBlockTrigger` instead; see below.

#### `CurrentBlockTrigger` (property on `CardPlayRound` / `CardPlayPool`)
The change event currently being **offered for block**, or null when no block window is open. This is what a **block reaction's `CardTriggers()`** must match against.

- Set at the start of `RequestBlockReactions`, restored (stack-safe) in the `finally` — a block card resolved inside the window runs its own steps, whose events open nested windows, and the outer window's remaining factions need their trigger identity back
- `RequestBlockReactions` recalculates tags *inside* its loop, after `CurrentBlockTrigger` is set and again after any block card resolves, so conditions are always evaluated against the open window
- Also drives the block prompt's trigger label and the "cannot block your own action" guard in `RequestBlock`

**Never match a block trigger against `CardPlayPool.LastNoneNewCardChangeEvent`.** That property deliberately skips `PlayCardChangeEvent` and `ActivateReactionChangeEvent`, so when it is read anywhere near an introduction event it reports the *earlier* event. That is what made every removal-blocker (e.g. UK's Destroyers) offer itself against StatusRadar being activated — a card activation is not a unit removal. Introduction events no longer open a block window at all, which removes that particular trap at the source, but the rule stands for every other window.

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
   PlayCardChangeEvent → applied (no window opens on it)
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
   ActivateReactionChangeEvent → applied (no window opens on it)
   Air Raid step executes: Create RemoveUnitChangeEvent
   ```

4. **RemoveUnitChangeEvent triggered**
   ```
   Germany/Japan/Italy check for blocks
     - Germany plays "Anti-Aircraft" (blocks the removal)
   ```

5. **Germany's "Anti-Aircraft" activates**
   ```
   ActivateReactionChangeEvent → applied (no window opens on it)
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

`IsBlockRequest` is only a marker — bare, its `MeetCondition()` returns an unconditional `true`; given a `CardType` it also tests the blocked event's source card type. The *second* condition is what decides which event this card can block, and it must test `CurrentBlockTrigger`. Without that test the card is offered against every event that opens a block window.

The marker must be `IsBlockRequest` **or a subclass of it**: `CardLogic.IsBlockReaction` is a bare `is Condition.IsBlockRequest` type test, so a sibling condition class is never offered a block window at all. Subclassing is also how a block card gets by with a single condition — `Condition.IsBlockRequestFromCard` folds the scoping test into the marker. And never wrap the marker in `Condition.Not`, which is a `CustomCondition` and destroys the type match.

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

### Reacting to a play or an activation

An introduction event opens no window, so a card that wants to react to "you played a card" or "the
opponent activated a card" cannot key on the introduction event itself. There are **two shapes**, and
which one a card needs is decided by *when* it must act relative to the played card's effect.

#### Kind A — act before the activated card's effect lands: a block reaction

Use this when the reaction must get in ahead of what the card does. The block window on the card's
first blockable step event is the only window that runs before the effect applies.

`UK/ResponseEnigmaCodeCracked` ("use when Germany activates a Status card, discard that Status card")
is this shape:

```csharp
protected override List<Condition> CardTriggers() => new List<Condition> {
    Condition.Build(new Condition.IsBlockRequestFromCard(Faction.GERMANY, CardType.STATUS), this)
};
```

- **Derive from `IsBlockRequest`, never sit beside it.** `CardLogic.IsBlockReaction` is a bare
  `is Condition.IsBlockRequest` test, and it is what earns `Tag.IsBlockReaction` and so any place in a
  block window at all. A sibling class compiles, reads correctly, and is never offered.
- The trigger reads `CurrentBlockTrigger`; the step reads `ActivationTrigger` and walks to
  `.SourceCardId` for the card that produced the event. `CurrentReactionTrigger` is **not set** inside a
  block window, so no `EventCondition` / `.Immediately()` machinery works there.
- **"Activated" needs no explicit test.** A Status/Response card played from hand runs no steps at all,
  and an introduction event never reaches `DoChangeEvent`, so any block window whose source card is a
  played Status card is necessarily an activation of it.
- **A block card need not block.** Enigma sets nothing on the event — it uses the block window for its
  *timing*, and its effect is a discard. Consequence: `RequestBlockReactions` loops
  `while (!changeEvent.IsBlocked)` and does not clear its `passed` set on a play, so the window takes one
  more turn after such a card resolves. Harmless — the spent card is gone from the options — but expect
  the extra prompt.
- **The cost is already paid by then, and the card still resolves.** Every activatable Germany Status
  card emits its discard/spend cost with `IsTrigger = false` first and its blockable payload second, so
  Kind A cannot fire before the cost. Nor does discarding un-resolve the card: `DeckState.DiscardCard`
  has never halted an activation in flight — `DoCard`'s step loop reads only `StepFinished` and
  `CardLogic.IsBlocked`, and only `IsCardBlocked` bridges an event's block onto the card
  (`ResponseASWTactics` is the sole user).
- **Accepted gap:** a card whose steps produce no blockable event opens no block window, and Kind A is
  simply not offered against it.

#### Kind B — modify the played card, acting after its step: a pool-scoped trigger

Use this when the reaction is about where the played card *ends up* rather than whether it resolves.
`Condition.FactionPlayedCard` is a pool-scoped `EventCondition`, so the card is offered in the
after-reaction window of **any** step of the played card.

`UK/ResponseRationing` ("instead of placing that card in your discard pile, shuffle it into your draw
deck") and `Soviet/StatusWomenConscripts` ("place that Build Army card on the top of your draw deck
instead of discarding it") are both this shape. Both defer the actual move to a one-shot
`MutatorRecycleAfterStep`, which fires `AFTER` the turn step:

```csharp
private PlayCardChangeEvent PlayedCard() =>
    CardPlayPool.ChangeEventsPool
        .OfType<PlayCardChangeEvent>()
        .LastOrDefault(pc => pc.TriggeringFaction == Faction
                          && DeckState.ForFaction(Faction).DiscardedCardIds.Contains(pc.SourceCardId));

protected override List<Condition> CardTriggers() => new List<Condition> {
    Condition.Build(new Condition.FactionPlayedCard(Faction), this),
    Condition.Build(new Condition.IsGameFlowStep(TurnStep.PLAY_CARD), this),
    Condition.Build(new Condition.IsFactionTurn(Faction), this),
    Condition.Build(new Condition.CustomCondition(() => PlayedCard() != null), this)
};

new CardStep(this, async () => {
    PlayCardChangeEvent playEvent = PlayedCard();   // the SAME helper the trigger used
    if (playEvent == null) return;
    ModifierRegistry.Register(new MutatorRecycleAfterStep(
        Faction, playEvent.SourceCardId, CardState.Id, GameFlow.Instance.TurnStep,
        RecycleDestination.ShuffleIntoDeck, "…description…", "…bulletin text…"));
    await Task.CompletedTask;   // the mutator does the work once the step ends
})
```

Five things to copy:

- **One predicate, used twice — and it must be the pool in both.** The window's trigger here is a *step*
  event (a deploy, a battle), not the play, so the step cannot read `ActivationTrigger`: it would find no
  `PlayCardChangeEvent` and the card would be offered only to no-op through its own error guard.
- **Still defer the effect.** Acting immediately moves the played card out of the discard pile and only
  then lets it resolve — `Tag.IsPlayed` no longer set for it, and the card redrawable in the same turn.
  The text is about where the card ends up. `MutatorRecycleAfterStep` fires at `FinishStep(PLAY_CARD)`,
  the end of the *turn* step, so registering it from any window inside that step works identically.
- **No `.InReactionWindow()` needed.** `EventCondition.RequiresEventContext` is always true, so
  `HasEventBasedTrigger` passes without it. That pitfall applies to a bare `CustomCondition` used
  *alone* — here the `FactionPlayedCard` in the same list already satisfies it.
- **`LastOrDefault`, not `First`.** A round can hold two plays by the same faction
  (`MutatorReallocateResources`, `StatusGuards`, `EventFlexibleResources`). The most recent is the one
  just played.
- **Keep the discard-pile clause, and scope to your own turn.** The clause is what excludes a
  Status/Response card played onto the table — `DeckState.PlayCard` files those into
  `StatusCardIds`/`ResponseCardIds` instead. And a round is one faction's one turn step, but
  `EventLendLease` makes another faction play a card inside the US's round, so `IsGameFlowStep` +
  `IsFactionTurn` are what stop pool scope honouring it for the rest of that round. Do **not** use
  `Condition.IsPlayCardStep`: it folds in `!HasPlayedCardThisTurnStep`, already false by the time any of
  these windows open.

**Accepted gap, both kinds:** a play whose steps open no window at all — step conditions unmet, the
player cancels the selection, or the step's only event is `IsTrigger = false` — offers no reaction to
the play either. Currently benign; a card added with an `IsTrigger = false` first step silently takes
this away.

### Acting at the beginning of your turn

There is **no `TurnStep.START` card window.** A card that reads *"at the beginning of your turn"*
(Volksturm, Superior Planning, Mobile Force, Defense of the Motherland) triggers on
`Condition.IsPlayCardStep` + `Condition.IsFactionTurn`, and is offered in the play prompt's side fan
alongside the hand:

```csharp
protected override List<Condition> CardTriggers() => new List<Condition> {
    Condition.Build(new Condition.IsPlayCardStep(), this),
    Condition.Build(new Condition.IsFactionTurn(Faction), this),
    // …the card's own availability conditions
};
```

- **`IsPlayCardStep` folds in `!HasPlayedCardThisTurnStep`**, so the card is only offered *before* the
  hand card — which is what "beginning of your turn" means once START is gone. Do not add your own
  `Not(HasPlayedCardThisTurnStep)`.
- **`IsFactionTurn` is not optional.** `IsPlayCardStep` alone is true for every faction during anyone's
  Play step, which tags the card `IsActivatable` off-turn and lets it surface in a depth-0
  after-reaction window on somebody else's turn.
- **Do not emit `SpendPlayActionChangeEvent`** unless the card really is played *instead of* a hand
  card. These four are in addition to the play. `CardPlayRound.Start` loops until
  `Condition.HasPlayedCardThisTurnStep.For(faction)` is true, so a free activation re-opens the play
  prompt with the hand still playable; a card that spends the play ends the step.
- **Override `CardLogic.IsFreePlayStepActivation => true`** on a card that leaves the play intact.
  Nothing infers it — a trigger list does not say what a step will spend — and it is what earns the
  card the foil-sweep emphasis in the play prompt (`FactionHandDisplay.LayoutFan` →
  `CardScene.SetEmphasized`), shown only while the card is actually activatable. A free activation
  that forgets it still works; it just sits in the fan looking like every card that costs the play.
- **Why not a window of its own.** The START round only raised a prompt when
  `DeckState.ForFaction(faction).ActivatableCardIds.Count > 0`, so the prompt appearing was proof the
  faction held a start-step card — for a face-down Response, the exact secret. This is the same leak
  the always-ask rule closes for reaction windows, and START was not covered by it. The play prompt
  opens every turn regardless, so folding these cards into it divulges nothing.

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
    await ModalStack.Current.Show(ModalConfig.Display("Discarded cards", items).WithAutoDismiss());

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
2. **Teams Take Turns, Factions Do Not Queue**: a window alternates team turns, opponent team first, one reaction per turn. There is no order between the factions of a team — they are prompted together and the first card chosen is the team's choice
3. **Steps Execute Sequentially**: Next steps only execute after all reactions to previous steps complete, AND only if at least one reaction was played
4. **Two Reaction Timings**: Block (before) and After (after) reactions provide strategic options
5. **Self-Response Allowed**: Factions can respond to their own team's actions, but not block their own
6. **Pass-Once Per Window**: Once a faction skips a reaction, they aren't asked again until a new reaction resets the window
7. **Priority Reset on Play, But the Turn Still Passes**: when any faction plays a reaction, all pass-locks are cleared before resolving it — everyone gets a fresh opportunity during its resolution. The turn pointer still flips, so the team that just played waits for the opponent's turn before using a second reaction
8. **Round Ends on All-Skip**: if neither team plays in two consecutive turns, `ContinueWithNextSteps` is skipped and `Finish()` is called at the top-level `DoCard`
9. **`Finish()` is Unconditional at Top Level**: `DoCard` calls `Finish()` whenever `reactionDepth` returns to 0 for the initial play — the old `hasRemainingActions` guard was wrong because skipped reactions don't consume their tags
10. **Reaction Windows are Trigger-Scoped**: `CurrentReactionTrigger` identifies which specific event opened the active window. Cards that can react to multiple different events (e.g. a card reacting to *any* sea battle in the pool) must use `CurrentReactionTrigger` to scope their targets to the correct event — not scan all matching events in the pool
11. **Activations Bind Their Trigger**: `DoCard` pins the triggering event onto the `CardLogic` as `ActivationTrigger` for the whole activation. Step logic reads that, not the pool — a live pool read is retargeted by any `Apply()` that lands in between
12. **Being Asked Proves Nothing**: any faction holding an unrevealed Response card is offered every reaction window whether or not it can react, so the prompt cannot be read as evidence of what the hidden card does. The option list, not the prompt's existence, is the signal
13. **Scoped Skips Never Cost a Visible Reaction**: *Skip rest of turn step* / *Skip rest of round* suppress only the windows that exist to hide information. A window where the faction holds a face-up Status card (or an already-revealed Response) survives the skip — it was never hiding anything
14. **Only Change Events Are Blockable**: introduction events (`PlayCardChangeEvent`, `ActivateReactionChangeEvent`) carry `IsTrigger = false` and open no block window. Playing a card and activating a reaction are declarations, not effects — you block the effect the card's steps produce, never the declaration itself. A card that wants to answer the *activation* keys on the first event that activation produces (`Condition.IsBlockRequestFromCard`), not on the declaration
15. **Introduction Events Open No Window At All**: neither blockable nor reactable. `DoCard` applies the introduction event and goes straight to the card's steps; every reaction window in the game is opened by `DoChangeEvent` on a real change event. Reacting to a play or an activation is expressed through the windows the card's own resolution opens — see the Kind A / Kind B section
16. **A Card Cannot React to Its Own Introduction** — structurally, with no special case needed. `RequestAfterReactions` once stripped the introduced card from both the offered options and the always-ask cover count; now no introduction opens a window, so the situation cannot arise
17. **React During the Card, Act After the Step**: a Kind B reaction fires in a window opened by one of the played card's own step events, so that card may still have steps to run. Anything that moves or re-files it must defer to an `AFTER` step mutator, or it will relocate the card and only then let it resolve

## Common Pitfalls

1. **Forgetting `IsTrigger = true`**: ChangeEvents produced by a card step must have this flag to be blockable and to open an after-reaction window. This is now load-bearing twice over: a first step whose only event is `IsTrigger = false` opens no window at all, so the Kind A and Kind B reactions to that card being played are silently never offered
2. **Not marking block reactions**: Block reactions need `IsBlockRequest` in their triggers
3. **Infinite loops**: Ensure reaction triggers have conditions that eventually become false
4. **Blocking yourself**: The system prevents self-blocking, but be aware of the rules
5. **Step prerequisites**: Make sure prerequisite steps are properly set for multi-step cards
6. **Double-ask bug**: `RequestAfterReactions` is called at every `DoChangeEvent` level. Without `_afterReactionPassedFactions`, a faction could be asked inside a nested reaction window AND again in the outer loop restart. Always use the shared pass-tracking set.
7. **Stale `hasRemainingActions` check**: Checking for remaining reaction tags to decide whether to `Finish()` is incorrect — a skipped faction still holds the tag. `Finish()` must always fire when returning to `reactionDepth == 0`.
8. **Matching a block trigger against the pool**: a block card's `CardTriggers()` must test `CardPlayPool.CurrentBlockTrigger`. Testing `LastNoneNewCardChangeEvent` matches inside *every* block window, so the card gets offered against events it has nothing to do with. Introduction events no longer open a block window, which retires the worst instance of this (a removal-blocker offered against a card activation), but not the pitfall.
9. **Testing for a reaction by its prompt**: a test that proves a card is *not* offered by hanging on an unexpected prompt no longer works — the always-ask rule raises a window for that faction anyway. Assert `promptoptions == 0` instead.
10. **A trigger list with no event-context condition in it**: `HasEventBasedTrigger` is `Any(c => c.RequiresEventContext)`, and `CanBeActivated` rejects the card at any `ReactionDepth > 0` — which every reaction window is — when it is false. The card compiles, reads correctly, and can never be offered, with no error. Any `EventCondition` (`FactionPlayedCard`, `UnitAboutToBeRemoved`, …) or `IsBlockRequest` satisfies it; a list of nothing but bare `CustomCondition`s does not, and needs `.InReactionWindow()` on one of them.
11. **Acting on the played card while it is still resolving**: a Kind B reaction fires in a window opened by one of that card's own step events, so it may still have steps to run. Moving the card there (recycle, discard, re-file) lands it in its new pile and only then finishes resolving it. Defer to an `AFTER` step mutator — see "Reacting to a play or an activation".
12. **Resolving a reaction while the team's other prompts are still open**: `TakeTeamTurn` must `Task.WhenAll` its peer groups before its caller runs `DoCard`. Resolving from inside a group task leaves another peer's prompt live while state mutates, and the two peers' GameMessage ids and state hashes stop agreeing. Same reason `OpeningDiscard` gathers in parallel but applies serially.
13. **Recording a withdrawn prompt as a pass**: a faction whose prompt was taken off the screen because a teammate used the team's reaction made no decision. Adding it to `_afterReactionPassedFactions`, or feeding its untouched `ReactionSkipScope` to `GameFlow.RecordReactionSkip`, silences a player over somebody else's choice. Both call sites test `withdrawToken.IsCancellationRequested`.
14. **Reading `LastChangeEvent` in step logic**: `CardPlayPool.LastNoneNewCardChangeEvent` / `LastChangeEvent` are live reads that any prerequisite `Apply()` between the card being chosen and its step running moves out from under you, so the read silently lands on the wrong event and the `is SomeChangeEvent` pattern fails with no error. Use `ActivationTrigger` for "the event I am reacting to". A *deliberate* scan of `ChangeEventsPool` is a different thing and is correct for Kind B — but then the trigger condition must scan identically, or the card is offered and no-ops.
