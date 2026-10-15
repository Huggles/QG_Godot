# Total War expansion — plan and status

The official **Total War** expansion for Quartermaster General WW2 (2nd edition), as an opt-in ruleset
chosen at game setup. With it off, the base game must behave **exactly** as before: same sims, saves,
unit and card ids, and multiplayer. It will ship as a **separate Steam DLC depot**: all of its code,
data and art live in `expansions/total_war/`, and its code builds into its own `TotalWar.dll`.

**Scope:** the rules engine first. Card content (165 cards), card-level test scenarios and tests follow
once the card texts are available, together with a second pass on the Bolster engine (M4).

Rules source: the Total War rulebook PDF (ARTG010, 12 pages). The summary below is enough to work from.

Line numbers in this file are approximate: they were taken while the code was moving.

---

## Status (as of M3, 2026-10-15)

| Milestone | State | Commit |
|---|---|---|
| M0a — DLL loading spike | Done on macOS (dev and exported build). **Windows self-test still owed.** | `be9f8b0` |
| M0b — session setting, DLL version stamp, DLC ownership | Done. Lobby/skirmish checkboxes not yet checked by hand. | `a58b55d` |
| Solution entry for `TotalWar.csproj` | Listed for the IDE, not built by the solution (see Build). | `fe4a197` |
| M1 — France and China | Done. | `e803c8c` |
| M1 follow-ups | France/China folded into the UK/US faction rows; strip fixes; strip plate and turn tint removed; quit-mid-prompt RPC fix. | `acaaad4`…`2907597` |
| M2 — Air Forces | Done: units, supply, Air Defense, Air Attack, Reposition, recall, rendering. | `5c4eb80` |
| Air Forces debug scenario + `.qgc` tests | Done. | `3ce4282`, `a26e0a4` |
| M3 — Air step, Air Power and Bolster card types | Done: AIR step via `RuleSet`, Deploy/Marshal/Air Superiority, minor-faction marshal, card and deck overlays (Add only), placeholder art, `.qgc` tests. | `cdafe03`, `73d466f` |
| **Next: M4** | Bolster cards. | |

**Branches:** `master` contains M0a–M3 (fast-forwarded to `fd05018`, 2026-10-15). Start each milestone on
its own branch from `master` (`feature/total-war-m4`, …). The old milestone branches
(`feature/total-war-m0a-dll-spike` … `feature/total-war-m3`) are all contained in `master`.

**Still owed (by hand):**
- Windows self-test of M0a (steps under Verification).
- A look at: the lobby and skirmish "Expansion: Total War" checkboxes; the faction strip with the
  France/China tabs; Air Force placement on the map and the fly animation (use the debug scenario).
- M3 in the GUI (skirmish with Total War ticked, or the debug scenario): the Air step prompt (a
  button modal); the card picker for a Marshal's discard; the country picker for Deploy/Marshal/Air
  Superiority (no subdued "rebuild" markers); the placeholder Air Power card in hand, shown greyed in
  the Play prompt; the "Discarded cards" modal after each Air action.
- Multiplayer with Total War on (verification step 6), now that M3 adds the AIR step.

**Deferred decisions:**
- **Air Power counts are placeholders:** 3 per country in `data/decks.json`, so the Air step can be
  played and simmed. Replace with the real counts with the card content.
- **The Air step prompt opens on public facts only** (a hand with cards, and a supplied Air Force or a
  space to deploy one), never on whether the hand holds an Air Power card, so it often offers only
  Pass. A client-side auto-pass (like `UNTIL_ACTIVATABLE`) would spare humans the click; not built.
- **Total War saves from before M3** don't line up: the AIR step shifts the stored step counter. Base
  saves are unaffected.
- **China card routing.** A Soviet card acting for China should prompt the Soviet player. Today every
  prompt for a minor faction goes to its patron (China → US). Handle with the card content.
- **Victory skip.** The base engine never applies "skip the Victory step if an enemy Army holds your home
  space", for any faction, so minor factions don't either. Fix it for everyone or not at all.
- **Air Forces in reserve** are not shown in the faction strip yet.
- The CLI hangs instead of exiting when bootstrap fails (unknown `scenario=`, bad `expansions=`). Predates
  this work.

---

## Architecture (as built)

Two halves:

- **Core** = the base game assembly and the base depot: the engine, with generic capabilities under
  generic names, plus the expansion plumbing in `scripts/C#/Expansions/`.
- **Expansion** = `expansions/total_war/` and the DLC depot: `TotalWar.dll` (code) and, from M7,
  `total_war.pck` (data and art, mounted at the same `res://expansions/total_war/…` paths).

**Rule: core never tests for "Total War".** Generic capabilities (minor factions with a patron, air
units, hand reactions, step-boundary windows, extra turn steps) switch on from data and from
`IExpansionRules`. Core never references expansion types.

### Repo layout
```
expansions/Directory.Build.props      stamps every expansion DLL with the game version it was built for
expansions/total_war/
  expansion.json                      manifest: id, displayName, assembly, steamDlcAppId (0 = not on Steam yet), minGameVersion
  TotalWar.csproj                     builds TotalWar.dll against the game assembly
  scripts/TotalWarRules.cs            IExpansionRules entry point: Configure adds the AIR step
  scripts/Air/AirStep.cs              the Air step: Deploy, Marshal, Air Superiority, minor-faction marshal
  scripts/Cards/AirPower.cs           ExecutionClass of the Air Power card (no steps)
  data/factions.json                  France/China rows (added) + NumberOfAirUnits for the six base factions (patched)
  data/setup.json                     starting Armies: France in WESTERN_EUROPE, China in CHINA
  data/cards.json                     the Air Power card definition
  data/decks.json                     3 Air Power cards per country (placeholder count)
  data/scenarios/Scenario_Debug_AirForces.json
  assets/factions/france|china/       placeholder flags
  assets/factions/<faction>/cards/    placeholder <Faction>_AirPower.png and <Faction>_Bolster.png
  assets/textures/Units/QGAirForce.png  placeholder Air Force sprite
scripts/C#/Expansions/                ExpansionManifest, ExpansionRegistry (+LoadedExpansion), ExpansionLoader,
                                      IExpansionRules (+ExpansionRegistrar), RuleSet (+ExtraTurnStep),
                                      TypeRegistry, ExpansionIds, ExpansionContent, ContentPaths, ExpansionSelfTest
scripts/C#/Game/GameOptions.cs        the session's enabled expansions
scripts/C#/GameMessages/GameMessageJson.cs   shared serializer options (expansion message types)
scripts/C#/Air/AirForces.cs           Air Defense, Air Attack, Reposition
```

### Boot (in `GameManager._Ready`, before any data or network)
1. `ExpansionRegistry.Discover()` scans `res://expansions/*/expansion.json` (id must equal the folder name).
2. `ExpansionLoader.LoadAll()` for each manifest naming an `assembly`:
   - finds the DLL: exported build `<install>/dlc/<Assembly>.dll` (install root = exe folder, or the folder
     holding the `.app` on macOS); dev/CLI/sim `res://.godot/mono/temp/bin/Debug/` (not
     `Assembly.Location`: Godot loads the game assembly from memory outside an export);
   - checks `minGameVersion` and the DLL's `GameVersion` stamp against `BuildInfo.Version`, and refuses a
     mismatch with a clear message;
   - loads it into the game assembly's `AssemblyLoadContext`, refuses global type names that shadow core,
     registers it with Godot via `Godot.Bridge.ScriptManagerBridge.LookupScriptsInAssembly` (public but
     undocumented), adds it to `TypeRegistry`, and runs each `IExpansionRules.Register`;
   - never crashes the game: a failure lands on `LoadedExpansion.Error`.
3. `LoadedExpansion.IsAvailable` = loaded cleanly and owned: Steam `IsDLCInstalled(steamDlcAppId)`, or no
   Steam to ask (editor, CLI, sim), or no DLC app id yet.

### Session setting
- The host resolves the session's expansions in `MultiplayerSession.StartNew`: a save's own, else CLI
  `expansions=`, else the menus (`GameManager.PendingExpansions`), else the scenario's `"expansions"` array.
  They travel on `StartSession` (and `RejoinService.BeginRejoin`); every peer fixes them in
  `GameOptions.SetForSession` before building any state, and stops with a reason if it can't play one.
- Saves store `Expansions`; the load screen and CLI refuse a save whose expansions aren't available.
- Lobby and skirmish show an `ExpansionToggles` checkbox per installed expansion. Lobby clients report
  what they can play; the host can't start while a connected player lacks a ticked expansion.

### Data overlays (`ExpansionContent`)
Only enabled expansions' files are read, and their rows are appended after the base data, so base ids
never shift.
- `data/factions.json`: a row with a new `Index` adds a faction; a row with an existing `Index` patches
  only the fields it names.
- `data/setup.json`: scenario-shaped; adds factions the scenario doesn't set up itself.
- `data/scenarios/`: listed in the menus next to the base scenarios.
- `ContentPaths.Resolve("textures/…")`: an enabled expansion's `assets/` first, then base `assets/`.
- `data/cards.json`: card definitions appended to `CardDataList` (setting it resets the by-name cache).
- `data/decks.json`: per-faction `Add` lists, instantiated after every base card, and `Remove` lists, whose
  cards leave their ids unused, so every base card that stays keeps its id. An expansion removing a card
  the deck doesn't hold stops the session. `Scenario_Debug_AirForces` deals the UK its whole opening hand,
  so the Air step tests don't depend on the UK's deck.
- Card art for expansion card types (`AIR_POWER`, `BOLSTER`) resolves through `ContentPaths`;
  `FactionData.LoadCardTextures` skips a type with no art, so the base game loads none.

### Session rules (`RuleSet`)
- `IExpansionRules.Configure(RuleSet)` (default no-op) runs in `GameOptions.SetForSession` on every peer;
  the result is `GameOptions.Rules`. Empty for the base game.
- `RuleSet.AddTurnStep(step, after, body)`: `GameFlow.DefaultTurnSteps` inserts the step after `after`;
  `ExtraStep` runs `BeginStep` (so a `CardPlayRound` is open and actions get reaction windows), then the
  body for the current faction, then `FinishStep`. `RestartStepBody` re-runs a body for a save taken on
  its prompt. A `StepSkippedException` out of the body ends the step.

### Air step (Total War, `AirStep.Run`)
- Asked only on public facts: the hand has a card, and the country has a supplied Air Force or a space
  to deploy one. Options: Deploy (Air Power card, then a space), Marshal (the Air Force, always asked
  even when there is one; then a space, its own allowed; then any card to pay), Air Superiority (Air
  Power card, then an adjacent space, then which enemy Air Force if several), Pass. Passing on an Air
  Force or space pick returns to the choice; the card to discard is required (exactly 1).
- The prompt is `InputRequest.AirStepRequestHandler`, a `SelectOptionRequestHandler` subclass (Kind
  "AirStep"), so the GUI, CLI and bots already answer it. Picks reuse SelectCard/SelectUnit/SelectCountry;
  identical cards (Air Power) and a single enemy Air Force are taken without asking. Country picks set
  `MarkRebuildTargets = false`.
- The cost is a `DiscardHandCardsChangeEvent` (`IsTrigger=false`); the action goes through
  `CardPlayPool.DoChangeEvent` (`IsTrigger=true`): `DeployAirForceChangeEvent`,
  `MoveAirForceChangeEvent(MARSHAL)`, `RemoveUnitChangeEvent(AIR_SUPERIORITY)`, or the no-op
  `AirSuperiorityChangeEvent` for a space with no enemy Air Force.
- Then, for the UK/US, each minor ally with a supplied Air Force: "Marshal the French Air Force" or Pass,
  paid from the patron's hand.
- Air Power and Bolster cards are never the card played (`CardLogic.CanBeActivated`, Lend-Lease's offer);
  they can still be discarded to satisfy the Play step.

### Minor factions (France, China)
- A minor faction is one whose faction data names a `PatronFaction`: pieces on the board, no seat, deck or
  turn. `Faction.FRANCE=8`, `CHINA=9` (after `ALL`).
- `StaticGameData.BoardFactions` (playable + minors) drives the board: supply/build tags, the sync hash,
  team scores, game results, CLI views. Turn order, cards and seats stay on `PlayableFactions`.
  Helpers: `MinorFactions`, `PatronOf`, `MinorsOf`, `ControllerOf`, `BoardFactionsForTeam`,
  `MaxFactionsPerSpace` (largest team on the board: 3 base, 5 with Total War).
- The patron's Supply step removes its minors' unsupplied pieces; its Victory step scores them on their
  own summaries. `PlayerFactionRegistry` routes a minor's prompts to its patron's seat.
- The faction strip folds minors into the patron's row: combined score, "(France)" on their VP lines, and
  a tab under the row with their army/navy counters.

### Air Forces (core, generic)
- `UnitType.AIR_FORCE` (after `ANY`). `UnitType.ANY` now means Army or Navy, so no base card can react to
  an Air Force.
- Created after every Army and Navy. Not in a space's `Units` map (so never occupying, battled,
  supplying or scoring); where one stands comes from its unit record. `ActiveUnitIds` returns ground
  units only; `ActiveAirForceIds`, `AirForceIn`, `CanDeployAirForce`, `RepositionTargets` and
  `SuppliedAirForcesAdjacentTo` cover Air Forces. An Air Force is supplied when its Army or Navy is.
- Events: `DeployAirForceChangeEvent`, `MoveAirForceChangeEvent` (`MARSHAL`/`REPOSITION`, same piece,
  fly animation). Removal reasons `AIR_DEFENSE`, `AIR_ATTACK`, `AIR_SUPERIORITY`, `REPOSITION_LOST`.
- `AirForces.ResolveAirDefense` runs in `CardPlayRound.DoChangeEvent` (and `CardPlayPool.DoChangeEvent`)
  after the card block window. Reposition runs from `RemoveUnitChangeEvent.AfterAppliedAsync` (a new
  host-only hook, skipped during replay). An empty Air Force pool triggers a recall
  (`UnitPoolShortfall.ResolveBeforeAirDeploy`).
- These follow-up removals and moves use `IsTrigger=false, RegisterInPool=false`, like the ground recall.

### Build
- `Quartermaster General.csproj`: `Compile/None Remove="expansions/**"`; target `BuildExpansions`
  (after Build) runs `dotnet build expansions/*/*.csproj -c $(Configuration) -p:GameAssemblyPath=$(TargetPath)`.
  `SkipExpansionBuild=true` turns it off.
- `TotalWar.csproj` uses explicit `<Import Sdk="Godot.NET.Sdk" Version="4.7.1">` so `GodotProjectDir` is
  the repo root: script paths `res://expansions/total_war/scripts/…`, output beside the game DLL, own obj
  folder under `.godot/mono/expansions/total_war/obj/`.
- The `.sln` lists `TotalWar.csproj` with `ActiveCfg` only (no `Build.0`), so the IDE loads it but the
  solution doesn't build it in parallel against a stale game DLL. Re-check after the IDE edits the `.sln`.
- Godot's export publish also builds `TotalWar.dll` (ExportRelease) but doesn't bundle it.

### Rules for expansion code
- Public core API only (all top-level core types are public; make an `internal` member public on purpose
  rather than adding `InternalsVisibleTo`).
- `partial` `GodotObject` subclasses (CardLogic, PresentationEvent) work from the DLL.
- Prefer core-generic events/requests. An expansion-owned message registers through
  `ExpansionRegistrar.AddMessage<TDto>("TW.…", factory)`.
- No `.tscn` whose scripts live in the DLL.

---

## Rules summary (rulebook)

**Turn sequence:** Play → **Air (optional)** → Supply → Victory → Discard → Draw.

**Setup and general changes:** each country draws 12 and discards 5. French Army in Western Europe,
Chinese Army in China. Discard step: discard at least 1 card or the team loses 1 VP. Reallocate
Resources costs 3 cards.

**New card types**
- **Air Power:** spent in the Air step.
- **Bolster:** a reaction played straight from hand, then discarded.
- Neither can be *the* Play-step card, but either may be discarded to satisfy the Play step.
- New triggers: "the Axis declares its intention to use a Bolster / Status card".
- A cost "discard a card of a given type" reveals only the type.

**France and China:** Allied minor countries, home spaces Western Europe (moves to the UK via Government
in Exile) and China; own supply; scored in the patron's Victory step; the patron (UK / US) checks their
supply, scores them, may additionally marshal their Air Force in the Air step, and repositions it. Cards
that don't name France/China never act on or react to their pieces. France and China have no deck or
hand: whatever would make them discard (Landkreuzers, Westwall, base Atlantic Wall after a French battle)
makes their patron discard instead (confirmed).

**Air Forces:** at most one per country per space, only with its own Army or Navy; supplied if that piece
is. In the Air step a country does at most one of:
- **Deploy:** discard an Air Power card; place an Air Force with one of its supplied Armies or Navies.
- **Marshal:** discard any card; move one of its supplied Air Forces to any space with one of its supplied
  Armies or Navies (the same space is allowed, to trigger a reaction).
- **Air Superiority:** discard an Air Power card; eliminate an enemy Air Force adjacent to one of its
  supplied Air Forces (a space with none is allowed, to trigger a reaction).

Air Defense, Air Attack and Reposition are built (M2); the Air step is built (M3).

**Pieces (Army / Navy / Air):** Germany 7/3/2, Italy 4/3/1, Japan 5/5/2, UK 5/5/2, Soviet 7/1/1,
US 5/6/3, France 3/2/1, China 2/0/1.

**Out of scope:** the Experimental Constructed Deck rules.

---

## Remaining milestones (each gets its own go-ahead)

### M4 — Bolster cards
Two kinds: **event-triggered** ("when you build a Navy…", "when the Axis declares…"), which fit the
existing block and after-reaction windows; and **step-timed** ("at the beginning of your Play step",
before/after a step), which have no window today. The engine removed its old START card round on
purpose (comment near `Condition.IsPlayCardStep`): a window that opens only when someone can use it
reveals that the card is in their hand.

1. `BolsterCardLogic` base class (core) whose play conditions are `CardTriggers()`. A minimal one (no
   steps, nothing else) exists since the German cards were added.
2. `CardLogic.CanBeActivated`: a hand Bolster can be used whenever its own triggers are met and it has
   executable steps (once-per-turn still applies). Event triggers also need an open block/reaction
   window; step-timed triggers need the matching step-boundary window. The depth-0 rule for table cards
   still in hand must not apply to Bolsters. M3 made `CanBeActivated` refuse every hand `AIR_POWER`/`BOLSTER`
   card (`CardTypeRules.IsPlayedFromHand`); lift that for Bolsters here.
   - **Step-boundary windows (core):** `StepBoundaryChangeEvent(step, BEGIN|END, activeFaction)` with
     `IsTrigger=true` via `CardPlayPool.DoChangeEvent` (game-mechanic trigger: the team on turn reacts
     second). BEGIN in `GameFlow.BeginStep` after `ChangeStepChangeEvent` and the BEFORE mutators; END
     while the step's `CardPlayRound` is still open (`FinishStep` runs after the round is nulled; see
     `TURN_STEP_COMPLETION_REFACTOR.md`). Conditions `Condition.AtStepBoundary(step, BEGIN|END,
     Own|Opponent|Any)`; "before step X" = BEGIN of X, "after step X" = END of X.
   - A boundary window opens only for (step, timing) pairs some card in the session's decks refers to
     (computed from public deck contents at load). Inside it, every seat holding any Bolster is prompted;
     `ReactionSkipPreference` lets players auto-pass.
   - The active player's own "beginning of your Play step" Bolsters are also offered in the Play prompt
     (the `IsPlayCardStep` pattern), without spending the play.
   - Supply and Victory steps apply removals/scores directly today; Bolsters triggering on those need them
     routed through `CardPlayPool.DoChangeEvent` (`IsTrigger=true`) when the expansion is enabled.
3. Block/after-reaction tagging (`GameStateCalculator`) accepts hand Bolsters; `IsPlayable` and the
   hand-play prompt exclude them (except the Play-prompt case above).
4. A hand Bolster is introduced with a new `UseBolsterChangeEvent` that never calls `board.CountPlay`, so it
   never spends the turn's play. The card goes to discard afterwards.
5. **Hidden information:** with the expansion enabled, the "always ask" rule (`HasHiddenResponseCards` /
   `ShouldOpenReactionWindow` in `CardPlayRound`) also counts "holds any Bolster in hand";
   `IsPubliclyVisibleTableCard` treats hand Bolsters as hidden; `FactionHandDisplay` highlights offered ones.
6. **"Declares intention to use a Bolster/Status card":** with the expansion enabled,
   `UseBolsterChangeEvent` and the Status-card `ActivateReactionChangeEvent` become block triggers;
   conditions `Condition.DeclaresBolsterUse(team)` / `DeclaresStatusUse(team)`. A blocked Bolster is
   discarded without effect; a blocked Status card can't be used this turn.
7. **Type-reveal costs:** a discard-a-card-of-type step shows opponents only the type.

Read the `card-reaction-system` and `reaction-specifics` skills first.

### M5 — Setup and step rule tweaks (Total War only)
Rule parameters on the `RuleSet`, supplied by `TotalWarRules`; core only reads them.
- Opening hand: draw 12, discard 5 (`StaticGameData.OpeningHandSize/OpeningDiscardCount`, `GameFlow`
  start-of-game draw, `Game/OpeningDiscard.cs`).
- Discard step: no card discarded → the team loses 1 VP (`Steps/DiscardStepHandlerDefault.cs`, via
  `ScorePointsChangeEvent`).
- Reallocate Resources costs 3 (`MutatorReallocateResources`).
- Deck overlay: done (`Add` since M3, `Remove` with the substitutes).

### M6 — Bots and sim
Bots answer every new prompt at random (pass included), no new rules or valuation.
- `Cli/InputRequestSpec.cs` mappings for Bolster windows. The Air step (Kind "AirStep", a SelectOption
  subclass, minor-faction marshal included), Air Defense/Air Attack and reposition are already answered,
  at random. The random bot marshals often and so burns cards: decks run dry about twice as often as in
  a base game (148 vs 61 "Deck is empty" lines, seed 42 d1).
- The bot's reaction default is `passChance: 0` (`Ai/AiSeatRuntime.cs`), which would spend every Bolster:
  the Air step and Bolster windows must allow passing. Check a hand Bolster offered in an `ActivateCard`
  window doesn't fall under `PreferValuableCardRule`.
- For correctness only: `ProjectionValuer.TeamDelta` and `VpMath` iterate the team's `BoardFactions`.

### M7 — Export and Steam DLC depot (when there's content to ship)
- Base export presets: `exclude_filter="expansions/*"`.
- `ExpansionRegistry.Discover` first mounts `<install>/dlc/<id>.pck` (`ProjectSettings.LoadResourcePack`).
- A "Total War DLC" export preset: resources under `expansions/total_war/*`, excluding `*.cs`, `*.csproj`,
  script `.uid`s, `bin/`, `obj/`; Export PCK/ZIP → `Builds/dlc/total_war.pck`. Copy `TotalWar.dll` from
  `.godot/mono/temp/bin/ExportRelease/`. Both are platform-independent: one depot for Windows and macOS.
- Steamworks (by hand): a DLC app under app 5159090 with one depot installing `dlc/total_war.pck` and
  `dlc/TotalWar.dll`; put its id in `expansion.json`; add the depot to the base app's build script.
- Release skill: export the pck, copy the DLL, fail if either is missing, run the exported build's
  `expansion_selftest` with `dlc/` in place; upload the third depot; add both to the preflight checks.

### Card content (follow-up, once the texts are available)
- **Germany: data in, effects not built.** The 22 cards from `TW_Germany.json` (printed #26–47) are in
  `data/cards.json` and Germany's `data/decks.json` row, each with a class under `scripts/Cards/Germany/`
  that extends its type's base (`EventCardLogic`, `EWCardLogic`, `StatusCardLogic`, `BolsterCardLogic`)
  and has no steps. Normalised on import: UniqueName/ExecutionClass prefixed by type like the base cards
  (`EventSeaLion`, `Bolster128cmFlakzwilling40`); `ECONOMIC_WARFARE` → `EW`; Landkreuzers and Westwall
  ("every time") get `MultipleActivationsPerTurn: true`; straight quotes. Land Battle (#40) and Ardennes
  Offensive (#42) reuse the base cards (Ardennes was defined in base data but in no base deck).
  12.8cm Flakzwilling 40's text ("eliminate an Italian Air Force") reads oddly; check it against the card.
- **United States: data in, effects not built.** The 25 cards from `TW_US.json` (printed #1–25), the same
  way, under `scripts/Cards/US/`. On import: the four `ECONOMIC_WARFARE` → `EW`; Anti-Japanese Volunteer
  Armies ("Each time") gets `MultipleActivationsPerTurn: true`. None overlapped a base card.
- **Soviet Union: data in, effects not built.** The 19 cards from `TW_Soviet.json` (printed #48–66), the
  same way, under `scripts/Cards/Soviet/` (Response cards extend `ResponseCardLogic`). Only fix needed:
  `PeopleSLiberationArmy` → `StatusPeoplesLiberationArmy`. Operation Zet has exactly Flying Tigers' text;
  check it against the card.
- **Japan, United Kingdom, Italy: data in, effects not built.** 22 + 21 + 19 cards (printed #67–128), the
  same way, under `scripts/Cards/Japan|UK|Italy/`; five `ECONOMIC_WARFARE` → `EW`. All six countries'
  cards are now in (127 Total War cards, 1000–1128 less 1040/1042). Check in the GUI that "Polskie Państwo
  Podziemne" renders: "ń" is outside Latin-1, unlike the other accented names.
- **Substitutes: data in, effects not built.** From `TW_Substitutes.json` (printed #129–133): each replaces
  the base card of the same name through a `decks.json` `Remove`, as `<BaseName>_TotalWar` (1129–1133,
  following base's `EventPlunder_Germany`). Dutch East Indies and Free France stay with the UK, American
  Volunteer Group Expands with the US. Two switch decks, each to the patron of the minor faction it now
  acts for (confirmed): Ledo and Burma Roads UK → US (China), Free French Allies US → UK (France).
- **Effects, the Easy batch: built.** 64 Status/Event/Response/EW cards, in the expansion only, on
  shared helpers in `scripts/Cards/TotalWarCards.cs`:
  - `DeployStep`/`BattleStep` read the card's faction when the step runs, because `OnActivate` builds
    steps before the card is dealt to a country (capturing `Faction` there gives `NONE`).
  - `DeployStepFor`/`BattleStepFor` act for a country the card names. France and China battle as
    themselves, so their targets are exactly what lies next to their supplied pieces.
  - "Allied"/"Axis" on a card means the six base countries unless the card names France or China (rule above).
  - Readings taken: Operation Pike's VP always scores, and its "If" gates only the discard. Senegalese
    Tirailleurs' "Allied Armies" include French ones. Heavy Tanks blocks only that battle (no
    `ImmuneForTurn`). Alpini protects for the turn, as the base removal-blockers do.
  - Test setup: `Scenario_Debug_TotalWarCards` deals each country its own cards;
    `tests/qgc/tw_cards_minor_and_extra_play.qgc`.
- **Effects, three more: built.** First counted as Medium, but existing pieces cover them:
  - Production Initiative and Strategic Planning shuffle with `ReorderDeckChangeEvent` and a
    host-drawn order (`TotalWarCards.ShuffleStep`), as the opening shuffle does.
  - Fu-Go Balloon Bombs returns to hand through `MutatorRecycleAfterStep` after the Play step, as
    Women Conscripts does: a card moved mid-play is re-armed and DoCard would run it again.
  - Tested by `tests/qgc/tw_cards_deck_and_return.qgc`.
- **Effects, Medium (8): each needs one small core hook.**
  - "Every time" Statuses are re-armed only per turn: Landkreuzers, Westwall, Anti-Japanese Volunteer Armies.
  - Reallocate Resources has no modifier seam: War Bonds, Victory Gardens (and M5's cost of 3).
  - Removal protection doesn't cover ELIMINATE/SUPPLY, and supply removals open no window: Élan.
  - The Discard step exposes no discarded cards: Attrition.
  - There is no strait-control override: Spanish Troops Seize Gibraltar.
- **Effects, Hard (6): each needs a new rule system.**
  - Supply through other countries' pieces: SHAEF.
  - Home/Supply spaces that move during play: Government Evacuates to Kuibyshev, Government in Exile.
  - Switching off Axis Statuses: Hobart's Funnies (pairs with Bolster Keep Calm and Carry On).
  - Axis-only adjacencies: Turkey Opens the Straits.
  - A "use at any time" window: Endless Expanses (with M4's windows).
- Unbuilt Event, EW and Status cards are playable and resolve with no effect (decided: keep them so);
  unbuilt Bolsters cannot be used until M4.
- **Numbering:** Total War cards are `1000 + printed number` (US #1–25 → 1001–1025, Germany 1026–1047);
  Air Power is 1000. Base cards are 1–146.
- `data/cards.json` entries per country plus one ExecutionClass per card under
  `expansions/total_war/scripts/Cards/<Faction>/`, including Air Power deck counts and the 5 substitutes
  (Dutch East Indies, Free France, Free French Allies, American Volunteer Group Expands, Ledo and Burma
  Roads; two of them switch decks).
- Built in batches per country, each followed by a sim batch.
- A second pass on the Bolster engine, driven by real cards.
- Scenarios and `.qgc` tests for minor factions, the Air step, Bolster windows and the hidden-information cover.

---

## Verification (every milestone)

1. **Base game unchanged (the key gate).** 10 sim games (seed 42, decision seeds 1–10). Their
   `results.jsonl` must be **byte-identical** to the baseline. `sim/runs/` is gitignored, so on a new
   machine first make the baseline from the current branch head (base behaviour was proven identical to
   `master` through M2), then compare every later run with `fc /b` (Windows) or `cmp`:
   ```
   cd sim
   dotnet build QGSim.csproj
   bin\Debug\net8.0\qgsim --seeds 42 --decision-seeds 1-10 --out runs\tw_base
   ```
   qgsim's default Godot path is the Windows one. If only a newer .NET runtime is installed, set
   `DOTNET_ROLL_FORWARD=Major`. The full 60-game set (seeds 42,1,7 × 1–20) is only for suspected drift.
2. **Total War smoke test.** The same command with `--game-arg expansions=total_war` (3 games is enough):
   no `stalled` or `result+errors` in `failures.txt`.
3. **Expansion self-test.** `Godot --headless --path . -- expansion_selftest=true` must print all PASS (7/7 since M3).
4. **Windows exported self-test (owed from M0a):**
   1. `/export-windows`;
   2. `New-Item -ItemType Directory -Force Builds\dlc; Copy-Item .godot\mono\temp\bin\ExportRelease\TotalWar.dll Builds\dlc\`;
   3. `Start-Process "Builds\Quartermaster General.exe" -ArgumentList '--headless','--','expansion_selftest=true' -Wait -PassThru -RedirectStandardOutput selftest.log`;
   4. expect exit code 0 and `PASS 7/7`. A release exe may write nothing to stdout; then read `godot.log`
      in the game's user-data `logs` folder.
5. **Rule tests.** `tests/qgc/*.qgc` (including the six `air_*` ones) are run by hand with
   `tests/run-cli.sh`; Claude doesn't run `run-cli.sh` or `run-all.sh` (standing project rule). Claude
   checks command sequences by piping them into the Godot CLI directly.
6. **Multiplayer (owed since M3):** two instances with Total War on — no hash divergence; a rejoining
   client rebuilds France/China and Air Forces; Air Defense prompts the defender's machine; a lobby member
   without the expansion blocks Start.
7. **Saves:** a base save from before this work loads; a Total War save round-trips (verified for Air
   Forces in M2; in M3, a save on the Air step prompt resumes on it, and one whose log holds an
   `AirSuperiorityChangeEvent` replays to the same hash).

**CLI helpers:** `scenario=Scenario_Debug_AirForces` enables Total War by itself. `force …` injects an
event without reactions; `api …` opens reaction windows (answer them with `pass`). Inject verbs include
`deployAirForce --faction F --country C`, `moveAirForce --faction F --unit N --country C` and
`airSuperiority --faction F --country C`.
`assert airforce <country> <faction>` checks for an Air Force. Saving refuses while the current step's
event pool is non-empty, so save at the start of a later step.

---

## Working agreements

- One milestone at a time, each with its own go-ahead, on its own branch from `master`.
- Keep sim runs small: the 10-game gate per milestone, not 60.
- The user checks visuals and UI by hand; say what to look at.
- Commits are signed; end commit messages with the Co-Authored-By line used so far.
- Don't push or merge without being asked.
