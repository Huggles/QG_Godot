# Lessons from the 2026-10-04 Standard Game

**Source:** `saves/save_2026-10-04-16-05-55.json`, a completed human game. HugglesNL played the Allies
(UK, USSR, US) and JasperBro the Axis (GER, JPN, ITA). The Axis won 186–155 in Round 13 on the
30-point lead. The data comes from a headless CLI replay (`load=` + `log 2000`) and from reading the
save JSON. This is one game (n=1): each lesson is a hypothesis, measured below.

## What happened

Allied income stayed at about 12 VP/round all game. Axis income grew from 8 to 17–24 VP/round.
**VP-rate growth decided the game.**

| Faction | Final | Per round (R1 → R13) |
|---|---|---|
| Italy | 71 | 2 3 3 3 4 4 5 6 6 7 7 7 **14** |
| Germany | 68 | 4 4 4 4 4 4 6 6 6 **9** 6 6 5 |
| Japan | 47 | 2 4 4 4 2 4 4 4 4 2 4 4 5 |
| US | 53 | 2 4 4 4 4 4 6 4 4 4 4 4 5 |
| UK | 52 | 2 4 4 4 4 6 4 4 4 4 4 4 4 |
| USSR | 50 | 4 4 6 4 4 4 2 4 4 2 4 4 4 |

## Lessons, and how they measured

All candidates were tested at seed 42, decision seeds 1–100, applied one-sided. Baseline:
**Allies 69% / Axis 31%**. At n=100 the 95% interval is about ±9pp. All 1500 games ran clean.

| Arm | Axis win % when applied to AXIS | Allies win % when applied to ALLIES |
|---|---|---|
| baseline | 31 | 69 |
| #1 status VP | **48** (+17) | **75** (+6) |
| #2 star capture | 31 (±0) | 70 (+1) |
| #4 scarcity | 30 (−1) | 69 (±0) |
| #1+#2 | 48 | 75 |
| #1+#4 | **52** | 74 |
| #2+#4 | 30 | 71 |
| #1+#2+#4 | **52** | 74 |

Runs: `sim/runs/lessons-<combo>-<side>/` (gitignored).

### #1 Status-card VP engines: CONFIRMED, large
Italy's Impero Italiano (R2) and Mare Nostrum (R8) took it from 2–3 to 7 VP/round. Its builds fed
both cards: armies in the Middle East and North Africa, navies in the Mediterranean and Bay of
Bengal. The bot couldn't see any of this. `ProjectionValuer.TeamDelta` priced only supply-star
income.

`bot_value_status=SIDE` adds `VpMath.StatusCardVpRateOn(team, board)` × `Horizon` to the
valuation. It reads the **fork's** Status piles, so a Status card the valuer plays onto a fork
counts. The live `ModifierRegistry` would miss that card.

Result: +17pp for the Axis and +6pp for the Allies. The Axis gains more because Italy and Japan
hold most of the VP Status cards. **Recommend:** rerun at 2000 games, then make it the default
(and include it in `AiSeatRuntime`).

### #2 Battle enemy units on supply stars: REDUNDANT
Germany used Land Battle + Blitzkrieg on Soviet armies in the Middle East (t25) and India (t37),
then built there. Rule `prefer_star_capture_battle` (off by default) fires on about half the
battle prompts, but changed **no** pick on the Axis side across 100 games. `prefer_valuable_target`
+ `ReactionForecast` already rank those targets top. **Recommend:** delete the rule, or keep it
off.

### #4 Card scarcity: NO EFFECT
The US lost 3+3 deck cards to Atlantic Wall, 1+1 to Amphibious Landings and 4+4 hand cards to
Reallocate Resources. Its deck ran out, and the last landing cost 1 VP.

`bot_value_scarcity=SIDE` prices hand+deck cards within the faction's remaining need at 3.0 VP,
and the surplus at 1.0. The need is the faction's own deck spread over the game; a flat 1.5
cards/round was tried first and left every bot in surplus, so not one pick changed. Even with
the per-faction pace it moved nothing beyond noise. **Recommend:** drop it unless a different
model comes along.

### #5 Reallocate Resources: CONFIRMED as a problem, but #4 doesn't fix it
Per-game `economy_stats` (now in every sim run and in `summary.txt`) show:

- **Japan and Italy reach for it most:** 2.45 and 2.20 uses a game, vs about 1 for the others.
  Their team does worse when they use it more. Italy at 2+ uses wins 26.8% (n=71), vs 42.9% at 1
  use. Japan at 2+ wins 28.8% (n=80), vs 45.5% at 0. That's correlation, not proof.
- **Italy loses 6.4 VP a game to an empty deck** (Japan 1.7, everyone else under 0.6), and ends
  with 0.84 deck cards left. Each use spends 4 hand cards that are refilled from the deck, plus
  the card it takes: about 5 cards out of a 30-card deck.
- **#4 doesn't change it:** use rates and empty-deck losses are the same within noise
  (Italy 6.39 → 6.21).
- **The valuer does charge for it.** On a fork, `ForceDiscardHandCardsChangeEvent.Mutate` discards
  the first 4 hand cards, so Reallocate is priced about 4 VP above a direct play. **But the bot picks
  the 4 discards at random** (no rule covers `ForceDiscardHandCards`). In one traced game, Germany
  discarded Atlantic Wall, Dive Bombers, Wolf Packs and Conscription.

**Recommend next:**
1. A discard rule that keeps the cards the valuer rates highest, and keeps Status cards.
2. A Reallocate gate that charges the deck cost, for factions whose deck can't cover the rest of
   the game (Italy, Japan).

### Not taken further
- Plunder pays more the longer you wait (Italy, t77, +5 for 5 units abroad). The bot plays greedy
  and has no "hold" concept.
- Reaction chains around Western Europe (Romanian Reinforcements, Atlantic Wall, Enigma at t72).
  The forecast already covers visible cards.
- UK–Japan naval trades in the Sea of Japan / South China Sea. No lesson.

## Side finding
Loading a completed save throws `ArgumentOutOfRangeException` in `GameFlow.ResumeAfterLoad` →
`DispatchTurnStep` (`GameFlow.cs:46`). After that, the CLI `board` output only shows home armies.
That matters for any tool that mines saves.

## Follow-up (same day)

- **#1 is now the default.** `bot_value_status` defaults to `all`, so in-game AI seats get it too.
  `none|AXIS|ALLIES` turns it off for an A/B. #2 (`prefer_star_capture_battle`) and #4 (scarcity)
  were removed.
- **`reallocate_for_builds`** (off by default) makes the bot use Reallocate Resources only to take a
  Build card. It vetoes Battle takes, and scores the activation down by however much a battle
  candidate inflated it. New baseline with #1 on: Allies 65% / Axis 35%.

  | Rule on | Axis win % | Allies win % |
  |---|---|---|
  | AXIS | 34 (−1) | |
  | ALLIES | | **55 (−10)** |

  Off the Axis it changes nothing: Japan and Italy were already taking Build cards. On the Allies
  it halves UK/US Reallocate use and costs about 10pp. For the Allied bot, Reallocate-for-battle
  is often its best move. **Left off.**

  Runs: `sim/runs/realloc-{base,AXIS,ALLIES}`.
