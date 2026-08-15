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

#### `RequestAfterReactions()`
Requests after reactions from all factions:
- Loops until no faction plays a reaction
- Each reaction is processed recursively
- After all reactions complete, continues with next steps

#### `ShouldOpenReactionWindow(Faction, List<int> options)`
The single gate deciding whether a faction is prompted at all, used by both the block and the
after-reaction path:

- A faction holding an unrevealed Response card is asked **even with nothing activatable**. Asking
  only when a reaction is available makes the appearance of the prompt proof of what the hidden card
  does, and its absence proof that there is none — so the window is always opened and the player
  passes with Skip. Status cards are face up and their triggers are computable by everyone, so they
  hide nothing and do not earn a prompt.
- A reaction prompt therefore also offers *Skip rest of turn step* and *Skip rest of round*
  (`ReactionSkipScope`, recorded by `GameFlow.RecordReactionSkip`). Those suppress **only** the
  information-hiding windows: a window where the faction holds a face-up Status card, or an
  already-revealed Response card, still opens.

#### `ContinueWithNextSteps()`
Checks all cards in the pool for executable next steps:
- Executes steps sequentially
- Each step triggers the full reaction chain
- When no more steps available, play step ends

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

```csharp
protected override List<Condition> CardTriggers()
{
    return new List<Condition> {
        Condition.Build(new Condition.IsBlockRequest(), this),  // Marks as block reaction
        Condition.Build(new Condition.FactionTeamUnitIsAttacked(FactionTeam.ALLIES), this)
    };
}

public override List<CardStep> InitializeReactCardSteps()
{
    return new List<CardStep> {
        new CardStep(this, async() => {
            // Block the attack by marking the last change event as blocked
            CardPlayPool.LastChangeEvent.IsBlocked = true;
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

## Key Design Principles

1. **Recursive by Nature**: Every reaction goes through the same flow as the original action, enabling deep chains
2. **Faction Order Matters**: Opponents always get first chance to respond
3. **Steps Execute Sequentially**: Next steps only execute after all reactions to previous steps complete
4. **Two Reaction Timings**: Block (before) and After (after) reactions provide strategic options
5. **Self-Response Allowed**: Factions can respond to their own team's actions, but not block their own

## Common Pitfalls

1. **Forgetting `IsTrigger = true`**: ChangeEvents must have this flag to allow reactions
2. **Not marking block reactions**: Block reactions need `IsBlockRequest` in their triggers
3. **Infinite loops**: Ensure reaction triggers have conditions that eventually become false
4. **Blocking yourself**: The system prevents self-blocking, but be aware of the rules
5. **Step prerequisites**: Make sure prerequisite steps are properly set for multi-step cards
