---
description: Board game rules for how Status and Response cards are played, triggered, and resolved — including reaction timing, ordering between teams, and edge cases. Use when implementing card trigger logic, resolving reaction ordering, or clarifying card interaction rules.
---

> **Authority:** This skill contains the official board game rules and takes **precedence over `card-reaction-system`** in any conflict. If the implementation described in `card-reaction-system` contradicts these rules, the rules here are correct and the implementation should be updated to match.

# The Play step is never skipped

On your Play step you must always do exactly one of three things:

1. play a card from your hand,
2. take an "instead of your play" card action, or
3. discard 1 card from your hand.

The third is the fallback, not a freely made choice: it is what happens when you pass, whether you
passed because nothing was playable or because you would rather keep your hand. A card action that is
explicitly *in addition* to your play does not satisfy the requirement — only spending the play does.

**If you hold no cards**, there is nothing to discard, and you lose 1 victory point instead.

Because passing always costs something, the play prompt is shown on every Play step, including when
none of your cards can be played: you still have to be given the choice, and told what passing costs.

# Using Status and Response Cards during Play

Status and Response cards must be played onto the table during the **Play** step of your turn before being used—they can't be used directly from your hand. (Playing a Status or Response card on the table is your Play step for the turn.) Once on the table the card is available to be used whenever conditions permit.

Use or applicability of Status or Response cards is always at the discretion of the country of the card, not necessarily the country that is affected.

The text of many Status and Response cards specifies a condition or prerequisite for use (*its trigger*) and may also specify a cost for use (for example, a discard). Any trigger and cost must be completed before the card's effect (the reaction) takes place.

Examples of triggers include "when you battle a land space" and "during your Victory step".

Other Status cards change some aspect of game play while these cards are on the table.

Usually the use of Status and Response cards is fairly obvious—however, there may be times when the interaction between cards may become tricky. Below are rules for resolving these situations.

## Reaction Rules

You are never required to use a reaction when a trigger occurs.

- Reactions are resolved immediately when their trigger occurs. For example: A "beginning of the Play step" reaction cannot occur once a card play is declared for the step.
- You may only use a Status card once per trigger (or only once per turn, if specified on the card).
- The two teams take turns reacting to any trigger, with the team that did not cause the trigger going first. If the trigger is a game mechanic (for example: "the beginning of your Play step"), then the team of the country that is taking its turn goes second.
- If a team has multiple reactions to the same trigger, only one is used at a time. Then the other team has a chance to use a reaction.
- A team with multiple reactions to the same trigger can use them in the order it chooses, as long as the other side has a chance to react in between.
- Reactions may trigger other reactions, which must be resolved before additional reactions to the original trigger are used.
- If a card's text does not have a specific duration, its effect occurs immediately and then ends; however, many cards have a duration that lasts until the end of the turn (ex. **Stalingrad**).
- A card that specifies "immediately after" may only be used when its trigger occurs, and is used as a reaction. For example, **Rasputitsa** may only be used immediately after an Axis Army is built or recruited; it may not be used to remove an Army placed on a previous player's turn.
- The beginning of your turn is prior to the Play step. Anything that happens at the beginning of the turn must be completed prior to the Play step.
- The beginning of a step is prior to the action of that step.
- Some cards specify that something happens during a step, meaning at the same time as the action of that step.

---

# Reaction Example 2

**Example:** Japan plays a **Sea Battle** and has the Response cards **Destroyer Transport** and **Surprise Attack** face-down on the table. *(In this example, the Allied team has no potential reactions available.)*

**Japan could:**

- Use **Destroyer Transport** as a reaction to **Sea Battle** to build armies adjacent to the battled sea space.
- Use **Surprise Attack** as a reaction to **Sea Battle** to battle an additional Navy and then an Army.
- Use **Destroyer Transport** and **Surprise Attack** both as reactions to the **Sea Battle** in whatever order Japan prefers.
- Use **Surprise Attack** as a reaction to **Sea Battle** to battle an additional Navy and then use **Destroyer Transport** to build armies adjacent to the second battled sea space. Japan would then finish **Surprise Attack** by battling an Army which could be adjacent to its newly placed armies.

**Japan could not:**

- Use **Destroyer Transport** as a reaction to the sea battle of **Surprise Attack** **after** resolving the land battle.

---

### Destroyer Transport

**Use when you battle a sea space. You may build one or two Armies adjacent to the space attacked.**

---

### Surprise Attack

**Use when you battle a sea space. You may battle an additional Navy, and then battle an Army.**

---

# Extended Card Play Examples

## Example 2

It is Japan's turn. Japan has Armies in Japan, China, and Southeast Asia, and a Navy in the Sea of Japan, and several Response cards on the table. The United Kingdom has Armies in Australia and India and Navies in the South China Sea and the Bay of Bengal, as well as several Response cards on the table.

### 1.

Japan plays a **Sea Battle** card to battle the Navy in the Bay of Bengal.

---

### 2.

The United Kingdom uses the **Destroyers** Response card to prevent that Navy from being removed.

#### Destroyers

**Use when a supplied United States or United Kingdom Navy is about to be removed. Do not remove that Navy this turn.**

---

### 3.

Japan uses the **Surprise Attack** Response card, first to battle the United Kingdom Navy in the South China Sea, then the Army in India. The United Kingdom does not respond, so both its Navy and Army are removed.

#### Surprise Attack

**Use when you battle a sea space. Battle a sea space; then battle a land space.**

---

### 4.

Japan then flips the **Destroyer Transport** Response card (also in response to its battle in the Bay of Bengal), and builds an Army in India.

#### Destroyer Transport

**Use when you battle a sea space. Build 1 or 2 Armies adjacent to the space just battled.**

---

### 5.

The United Kingdom flips the Response card **Loyal to the Crown** to immediately eliminate the Japanese Army just built.

#### Loyal to the Crown

**Use immediately after an Axis Army is built in India, Australia, or Canada. Eliminate the Army just built.**

---

### 6.

Japan is entitled to build a second Army by **Destroyer Transport**, and builds it in India.
