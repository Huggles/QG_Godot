# Lookahead, enemy turns and unit value (2026-10-11 – 10-12)

**Goal:** make the bot play board-manipulating Status cards (Blitzkrieg, Dive Bombers, Radar…) more,
by letting it look further ahead than one card.

All arms ran at seed 42, decision seeds 1–100, `Scenario_Basic`. The baseline is
**Allies 65% / Axis 35%** (`runs/la_ab_none`). At n=100 the 95% interval is about ±9pp. Every arm
ran clean. With every new switch off, 20 games stay byte-identical to master; this was checked
after each code change.

| Arm (applied to one side) | Switches | Axis win % on AXIS | Allies win % on ALLIES | Time per 100 games |
|---|---|---|---|---|
| baseline | — | 35 | 65 | ~6.5 min |
| 3-turn lookahead | `bot_lookahead` | **49** (+14) | **43** (−22) | ~31–36 min |
| lookahead + enemy turns | `+ bot_enemy_turns` | — | 49 (37 games, stopped) | ~2× lookahead |
| material 1.0 VP/unit | `bot_material` | **53** (+18) | 61 (−4) | ~6 min |
| material 0.5 VP/unit | `+ bot_material_vp=0.5` | **55** (+20) | 60 (−5) | ~9 min |
| material, front line | `+ bot_material_mode=front` | **66** (+31) | **46** (−19) | ~9 min |
| reach 0.5 | `bot_reach` | **74** (+39) | 71 (+6) | ~6 min |
| material + reach | `bot_material` `bot_reach` | **77** (+42) | **76** (+11) | ~9 min |

Both sides with material and reach on (`runs/uv_both_all`): **Axis 69% / Allies 31%**. Games got
shorter: 14.4 rounds against 17.7, and 79 of 100 games ended on the 30-point lead, against 59.

Runs: `sim/runs/la_ab_*`, `sim/runs/et_ab_ALLIES` (partial), `sim/runs/uv_*` (gitignored).

## 1. Three-turn lookahead: helps the Axis, hurts the Allies

`TurnLookahead` (`bot_lookahead`, `bot_lookahead_depth=3`, `bot_lookahead_beam=3`) scores each
play as the best line of the faction's own next turns. Each turn is a fork with the supply step
taken, the turn moved on, and Status cards re-armed (`GameSituation.Rearmed`). The other factions
pass, and nothing is drawn.

It does what was asked. Axis Status plays moved toward board manipulation:
- Blitzkrieg went from 2% to 20% of games, Dive Bombers from 2% to 22%, Synthetic Fuel from 0% to 31%.
- On the Allied side, Frontal Assault went from 1% to 33% and Stavka from 5% to 29%.

But it cost the Allies 22pp. **The cause:** a card whose payoff comes on *enemy* turns gets a
blind 0 in a line where the enemy passes. Such a card is "free", so it beats every card the
projection rates slightly negative:
- Radar went from 6% to 56% of games, Resistance from 34% to 82%.
- Response plays fell from 6.3 to 4.6 per game.

## 2. Enemy-turn value: the measurement works, the board value didn't

`EnemyTurnValue` (`bot_enemy_turns`) prices a Status or Response card over the enemy turns to
come. It plays every card in each enemy's hand and deck pile, taken as one pile, on two forks:
one with the card on our table and one without. Block windows are now forecast too
(`ReactionForecast.Block`). No card is flagged; the card's own triggers decide everything.

It works for Responses. Stalingrad was triggered by 8 of the 37 cards in Germany's pile, worth
+2 VP/round per firing. It never worked for Radar, which was q=0 in every game. **The reason:**
`ProjectionValuer.Value` gave a unit no worth of its own. A Navy off a supply star is worth 0, so
saving one was worth 0, so Radar's 2-card cost always lost. It's the same blind spot that let
Japan's Surprise Attack sink the North Pacific fleet unopposed. The A/B was stopped at 37 games.
The enemy-turn measurement has to be re-measured once units are worth something.

## 3. Unit value: the biggest win so far

`ProjectionValuer.Value` gets two optional terms:

- **Material:** `bot_material`, `bot_material_vp`, `bot_material_mode=flat|front`. Each piece is
  worth k VP, the cost of building it again. In `front` mode a piece next to a space an enemy holds
  is worth the full k, and any other piece half of it.
- **Reach:** `bot_reach`, `bot_reach_weight=0.5`. Per faction, the best income one build could add
  now (`VpMath.BestBuildVp`, from the board's Buildable tags), counted as `weight × horizon`. The
  enemy's reach counts against us, so cutting it is worth something.

Findings:
- **Reach is the broad fix.** +39pp for the Axis, +6pp for the Allies, at no extra cost in time.
- **Material helps the Axis whatever the level.** 0.5 and 1.0 are the same within noise; having a
  piece value at all is what matters. For the Allies it's flat to slightly negative.
- **Front-line material cuts both ways.** It is the best Axis material arm (+31) and clearly bad
  for the Allies (−19). One hypothesis: the Allies win by spreading out to many stars, and valuing
  contact points twice makes them hold the line instead.
- **Material and reach together** are the best arm for both sides: +42 Axis, +11 Allies.
- **With both bots on**, the balance flips to Axis 69 / Allies 31, and games end sooner. Two
  readings, not yet separated:
  - Stronger play favours the Axis in this scenario. That's a balance question for the game, not
    the bot.
  - The terms suit the Axis's compact fronts better than the Allies' wide map.

## Recommendations / next steps

1. **Make reach the default** after a 2000-game confirmation, possibly together with flat material.
   Check `AiSeatRuntime` picks it up.
2. **Re-run the lookahead and enemy turns with reach and material on.** Section 2 says enemy turns
   were starved of a unit value, not wrong. Radar's `ENEMYTURNS` lines should now be non-zero when
   an Axis battle can reach a supplied US Navy.
3. **Cut the cost of the lookahead before any default.** It costs 3–6× the run time. The
   enemy-turn cache is in (`EnemyTurnValue.NewDecision`) but hasn't been timed. Beam 2 and sampling
   the enemy pile are the next levers.
4. **Watch the Allies under `front`.** If the wide-map hypothesis holds, a reach weight that grows
   with the number of stars in reach may serve the Allies better than front material.
5. **Known limits:**
   - The lookahead skips Status cards that fire at the victory step but aren't income cards
     (Scorched Earth, Women Conscripts, Shvernik, Rationing).
   - The enemy-turn value assumes the enemy plays a random card from its pile.

## 4. Reallocate Resources overuse: three derived prices, none reach ~3 per game

The target was about 3 Reallocate uses per team per game. Where it stands:
- The baseline Allies are already there (3.1), but the baseline Axis use 6.7.
- Material + reach raises it to 5.2 (Allies) and 8.4 (Axis).
- Adding the lookahead raises it to about 8 (Allies, worst-10 seeds).

The cause: 5 cards at a fixed 1 VP each buy a Build from the deck, whose worth material and reach
have inflated. The lookahead re-offers a fresh Build at every turn of its line.

Two new valuation terms, both off by default:

- **Card worth:** `bot_card_value`. A card is worth the mean single-play value of the faction's own
  hand, never less than 1 (`ProjectionValuer.CardWorth`, cached per faction while the board stands
  still).
- **Scarcity:** `bot_scarcity`. Each card a faction falls short of its remaining own turns costs
  `CardWorth + 1`: the lost play plus the VP an empty hand forfeits (`ProjectionValuer.Shortfall`).

| Arm (100 games) | Allies win / Reallocate per game | Axis win / Reallocate per game |
|---|---|---|
| material + reach | 76 / 5.2 | 77 / 8.4 |
| + card worth | 77 / 5.0 | 74 / 8.2 |
| + scarcity | 77 / 5.2 | **79** / 8.1 (Italy empty-deck loss 2.9 → 2.3 VP) |
| + card worth + scarcity | **79** / 5.0 | 78 / 7.9 |

On the worst-10 lookahead seeds, card worth + scarcity cut Reallocate from 8.1 to 6.3 (B) and
from 7.9 to 5.7 (A), and the average margin improved by 3–5 VP.

**Findings:**
- Pricing barely moves Reallocate without the lookahead. The bot picks it when its hand *looks*
  worthless: Responses, delayed Status cards and events the projection values at about 0. In that
  state the average-play price is also at its floor of 1.
- Win rates don't drop with more Reallocate.
- **Next:** log the best alternative's value at each Reallocate decision. If it is mostly ≤ 0, the
  fix is valuing those hand cards, not the price of Reallocate.
- Card worth + scarcity is still the best overall stack (79 / 78), and its cost without the
  lookahead is small.

## 5. Deck tempo, and a methodology warning

**Diagnostic** (70 verbose games, scores read from the `bot_answer` JSON events):
- **Baseline:** 65% of Reallocate picks beat a hand whose best card scored ≤ 0. These were mostly
  Sea and Land Battles with worthless targets, and Bravado.
- **Full stack:** 73% of picks beat a *positive* but small alternative (median +2 against
  Reallocate's +14). With the lookahead it was +8 against +27.
- **The reason:** Reallocate plays the best Build or Battle in the whole deck, which the
  projection credits in full but charges as one average card.

**Deck tempo:** `bot_deck_tempo`, `ProjectionValuer.ChargeDeckTempo`, `FactionRecord.ProjectedCharge`.
A card played straight out of the deck is charged the part of its gain it would still have earned
when drawn normally: `gain × max(0, 1 − (deck ÷ 2) ÷ rounds left)`. It only ever fires on
Reallocate's deck pulls.

| Arm | seed 42 (100 games): Allies win / Reallocate per game | seed 7 (50 games): Allies win / Reallocate per game |
|---|---|---|
| baseline | 65 / 3.1 | 72 / 4.7 |
| full stack (material + reach + card worth + scarcity) | 79 / 5.0 | **88** / 4.6 |
| full stack + deck tempo | 52 (!) / 4.6 | **88** / 4.3 |

On the Axis side (seed 42), deck tempo gives 71 / 6.7, against 78 / 7.9 without it. On the worst-10
lookahead seeds Reallocate stays at about 6.

**Deck tempo is neutral.** It cuts Reallocate by about 7% and doesn't change the win rate. The
seed-42 −27pp did not replicate on seed 7.

**Methodology warning.** Every arm reuses the same deal and bot seeds, so a change that leaves
early decisions alone replays mostly the same games. Agreement between arms on one seed is
therefore not independent confirmation. A change that perturbs mid-game decisions effectively
re-rolls the trajectories, and on a single deal that alone can swing results by 20pp or more.
**Future A/Bs: use several deals (e.g. `--seeds 42,7,13`), and treat single-deal gaps under
about 25pp as unconfirmed.**

**Reallocate target.** About 3 per game is not a stable bot property even without any change:
the baseline Allies use it 3.1 times on seed 42 and 4.7 on seed 7, the baseline Axis about 7.
Pricing alone (card worth, scarcity, deck tempo) never moved it by more than about 25%. Reaching
3 would take either valuing the hand cards the projection scores near 0, or a rules change (the
4-card cost).

**The full stack replicates:** +14pp on seed 42 and +16pp on seed 7 for the Allies.
