# Localization inventory

This file lists every place that still shows English to the player, so the remaining translation work can be planned and ticked off. It was surveyed on 2026-11-24, after the main menu and Settings were translated.

Counts are approximate numbers of distinct strings. Word counts matter most for data text (cards, tutorials), because that is where translation effort goes.

## Summary

| # | Area | Where the text lives | Size | Difficulty |
|---|---|---|---|---|
| 1 | Menu screens (remaining) | `.tscn` + `scripts/C#/Menu` | ~70 scene + ~110 code strings | Easy |
| 2 | In-game HUD and dialogs | `scenes/userinterface/**` (`.tscn` + `.cs`), `scripts/C#/UI`, `Errors`, `Connection` | ~45 scene + ~125 code strings | Easy–medium |
| 3 | Card prompts / guidance / option labels | `scripts/C#/Cards/**`, `expansions/total_war/scripts/**`, `Air/` | ~200 guidance + ~60 titles/options + ~40 VP reasons | **Hard** (sent over the network, English grammar in code) |
| 4 | Game history and action labels | `ChangeEvents/*.SummaryText()`, `GameMessages/*` | ~45 + ~35 | **Hard** (sentence assembly, some sent over the network) |
| 5 | Card names and card text | `assets/data/QGData_Cards_V2.json`, `expansions/total_war/data/cards.json` | 256 names, 249 texts, **~5,200 words** | Medium (volume) |
| 6 | Tutorials | `assets/data/tutorials/Tutorial_0{1,2}.json` | 116 strings, **~2,450 words** | Medium (volume, BBCode) |
| 7 | Names: factions, countries, card types, turn steps | data JSON + `FactionData`, `CardTypeDisplay`, `TurnStepText` | 6+2 factions (+adjectives), 53 countries, ~10 card types, 5 steps | Medium (Dutch adjective inflection) |
| 8 | Scenario titles and descriptions | `assets/data/scenarios/*.json` | 17 (most are debug-only) | Easy |
| 9 | Sound-effect names | `assets/audio/sfx/settings.json` | 5 | Easy |
| 10 | Card art with baked-in words | `assets/factions/*/cards/*.png`, `expansions/total_war/assets/...` | ~60 images, ~10 type words ("Event!", "Build Army"…) | **Art work** |
| — | Not to translate | Steam store/achievements (Steamworks backend), debug overlays, CLI/sim output, logs | — | — |

## 1. Menu screens (remaining)

Scene text, as `text`, `ButtonText`, `tooltip_text`, `placeholder_text` and option items:

| Scene | Strings |
|---|---|
| `MultiplayerLobby.tscn` | 18 |
| `SkirmishScreen.tscn` | 16 |
| `HostOptionsDialog.tscn` | 13 |
| `GameMenuModal.tscn` (in-game Esc menu) | 6 |
| `JoinGameScreen.tscn` | 6 |
| `TutorialScreen.tscn` | 5 |
| `JoinOptionsDialog.tscn` | 5 |
| `LeaderboardDialog.tscn` | 3 |
| `InviteFriendsDialog.tscn` | 3 |
| `DisclaimerScreen.tscn` (incl. one long BBCode body) | 3 |
| `LoadGameScreen.tscn` | 2 |
| `SteamFriendLobbiesScreen.tscn` | 2 |
| `MenuNotice.tscn`, `MenuToast.tscn` | 3 |

Code-set text in `scripts/C#/Menu`:

| File | Strings |
|---|---|
| `MultiplayerLobby.cs` | ~24 |
| `InviteFriendsDialog.cs` | ~16 |
| `SteamFriendLobbiesScreen.cs` | ~14 |
| `SkirmishScreen.cs` | ~10 |
| `LoadGameScreen.cs` | ~9 |
| `JoinGameScreen.cs` | ~9 |
| `LeaderboardDialog.cs` | ~5 |
| `GameMenuModal.cs` | ~5 |
| `VictoryScreen.cs` | ~4 |
| others | ~10 |

Examples: status lines (`"Loading rankings…"`), notices (`"Could not create a Steam lobby"`), `"Normal AI"` / `"Hard AI"`.

Notes:
- `TutorialScreen.tscn` repeats the main menu's kicker and title. Reuse the `MENU_KICKER` and `MENU_TITLE_*` keys.
- `MenuToast` and `MenuNotice` upper-case their button text with `ToUpperInvariant()`. Pass translated text, as `MainMenu` now does for the invite toast.
- `VictoryScreen` shows `EndReason`, which arrives from the host as English text. See the network section below.
- Placeholder captions in shared button scenes (`"Action"`, `"Command"`, `"Jingle Bells"`) are always overwritten. Leave them.

## 2. In-game HUD and dialogs

Scenes:

| Scene | Strings |
|---|---|
| `Chat/ChatBox.tscn` | 7 |
| `user_interface.tscn` (debug toggles) | 4 |
| `Connection/ConnectionLostDialog.tscn` | 4 |
| `InputTimer/InputTimerDisplay.tscn` | 2 |
| `DeckComposition/DeckCompositionPanel.tscn` | 3 |
| `FactionInfo/ScoreHistoryDialog.tscn` | 2 |
| `FactionInfo/PileDetailPopup.tscn` | 2 |
| `CommanderMessage/CommanderMessage.tscn` | 2 |
| `PresentationModal/PresentationModal.tscn` | 1 |
| `RoundTrack/RoundTrack.tscn` | 1 |

Numbers, "ABC" placeholders and symbols in other scenes can be left as they are.

Code:

| Code | Strings | Examples |
|---|---|---|
| `scenes/userinterface/**/*.cs` | ~55 | `MiniMenu` ("Menu (Esc)", `$"Show the {faction} hand"`, which uses an enum ToString), `FactionInfoRow` ("Victory points", `$"{adjective} Discard Pile"`), `ScoreHistoryDialog`, `ChatBox`, `PlayersPanel`, `RoundTrack` (`$"Round {round} of {max}"`), `TurnSequenceStrip`, `ModalConfig` (Apply/Cancel/Close defaults), `PresentationModal`, `InputRequest.cs` ("Your input", "Waiting on … input...") |
| `scripts/C#/UI` | ~30 | `TriggerContextDisplay`, `ReactionSkipScopeButton`, `TurnBadge` (`"{faction}'s turn"`, an English possessive), `FactionIntro`, `ShowActionButton`, `SelectionSkipButton` |
| `scripts/C#/Input` | ~22 | `InputManager.BannerText`, `CardPick` (hand-rolled plurals), `InputHandlerDiscard` |
| `scripts/C#/Errors/ErrorPopup.cs` | ~22 | |
| `scripts/C#/Connection/*` | ~19 | reconnect messages, `RejoinService` refusal reasons (sent to the joining peer) |
| `scripts/C#/Game/*` | ~23 | `TurnStepText` (5 step names + rules), ranked bonus reasons, `GameFlow` end reasons |

`PlayerInfoDisplay` hard-codes the faction names a second time. Point it at the faction-name lookup instead.

Out of scope: `DebugOverlay` and `GameStateDetailPanel`, which are debug-only.

## 3. Card prompts, guidance and options (largest code area)

- **201 `WithGuidance("…")` calls.** About 120 are in core card classes and about 80 in Total War. Example: `"Select an army or empty land country to attack"`.
- **~60 InputRequest titles and option labels.** Examples: `"Choose an action"`, `"Discard top 2 cards from draw deck"`.
- **~40 VP reasons** (`new VPEntry(…)`). Example: `"armies on Ukraine, Kazakhstan and/or Russia"`.
- **~35 card action sentences.** Example: `$"{Faction.WithPlayer()} plays Radar: …"`.
- **Air step text and mutator labels and bulletins** (`Air/`, `Mutators/`).

Why this is hard:
- `CardStep.GuidanceSentence` builds sentences out of parts, as player + verb ("may" / "is unable to" / "chose not to") + guidance with its first letter lower-cased. That only works for English word order. Each combination needs to be a whole translatable sentence with placeholders.
- Much of this text is **built on the host and sent to clients as finished English**. A Dutch client in an English host's game would see English, and the reverse. See the network section below.

## 4. Game history and action labels

- `ChangeEvents/*.SummaryText()`: ~45 sentences (`$"{faction} battled in {country}"`, "discarded {n} hand card(s)").
  - English word fragments are glued together: "built"/"recruited"/"deployed", "a Navy"/"an Army", and `WithArticle()` for a/an.
  - `$"Turn step changed to {NewStep}"` shows an enum ToString.
- `GameMessages/*`: "Commander: ", "Bulletin: ", "Discarded cards".
- History rows are rebuilt on each peer, so they can be translated locally once the sentences become keys. The exception is `TriggerSummaryText`, which is sent.

## 5. Card names and texts (data)

| File | Cards | Card texts | Words |
|---|---|---|---|
| `assets/data/QGData_Cards_V2.json` | 125 | 121 | ~2,480 |
| `expansions/total_war/data/cards.json` | 132 | 131 | ~2,760 |

- The fields are `Label` and `Text`.
- Suggested keys: `CARD_<UniqueName>_NAME` and `CARD_<UniqueName>_TEXT`.
  - The data keeps its English as the source.
  - The game looks up `Tr(key)` and falls back to the JSON text when the key is missing.
- Card text mentions countries and nationalities inline ("Recruit an Italian Army in the Balkans"). Translate each card text as a whole sentence rather than substituting names into it.
- This is the biggest translation job by volume. It is also rules text, so it needs a careful translator who knows the board game's Dutch terminology, if a Dutch edition exists.

## 6. Tutorials (data)

| File | Steps | Strings | Words |
|---|---|---|---|
| `Tutorial_01.json` | 57 | 58 | ~1,390 |
| `Tutorial_02.json` | 57 | 58 | ~1,060 |

- The strings are `title`, `steps[].text` and `steps[].note`.
- The text contains BBCode (`[b]…[/b]`) and `\n`.
- Suggested keys: `TUTORIAL_01_STEP_<n>`.
- Tutorial messages are sent to clients as text (`ShowCommanderMessagePresentationEvent`), but tutorials are single-player, so local lookup is fine.

## 7. Names

| Names | Count | Source | Used through |
|---|---|---|---|
| Factions | 6 base + Total War (France, …) | `QGData_Factions_V2.json` (`Label`), `expansions/total_war/data/factions.json` (`Label`, `AdjectiveLabel`) | `FactionDisplay.Label()` / `WithPlayer()` |
| Faction adjectives | 6, hard-coded | `FactionData.cs:104` | |
| Countries | 53 | `QGData_Countries_V2.json` (`Label`) | `CountryState.Label` |
| Card types | ~10 | `CardTypeDisplay.Label()` | |
| Turn steps | 5 | `TurnStepText` | |
| Expansion name | 1 | `expansion.json` (`displayName`) | |

- Translating names through these few lookups covers every place they appear.
- **Dutch catch:** adjectives inflect ("Duits leger" but "Duitse vloot"). Sentences that slot `{adjective}` in front of a noun need care per language. It is safer to give keys like `DISCARD_PILE_OF` with the faction name instead.
- The map image has no baked-in names; country labels are drawn from data.

## 8–9. Scenarios and sound names

- **Scenarios:** each has a `title` and `description`.
  - 4 are player-facing: Standard Game, One Round Blitz, All Cards In Play, and the 2 tutorial scenarios.
  - The 11 `Debug…` scenarios can stay English.
- **Sound-effect names:** `assets/audio/sfx/settings.json` holds 5 labels, shown on the Settings → Audio tab ("Input Request", "Ping", …).

## 10. Card art with baked-in words

- About 60 card images have the card type painted into the art: "Event!", "Build Army", "Land Battle", "Status", "Response", etc. These are per faction × type, in `assets/factions/*/cards/` and `expansions/total_war/assets/factions/*/cards/`.
- Translating them needs either per-language image variants or art without text plus a drawn label. Godot supports image remaps per locale (Project Settings → Localization → Remaps), so variants would be easy to wire in.
- Card backs may also carry text; check them.

## Network: text that leaves the host as finished English

These carry English strings over the network, so each player can't see them in their own language. The fix is to send a **key plus arguments** and translate on arrival. For example, `{ key: "CARD_RADAR_PLAYS", args: [faction] }`, with faction and country passed as IDs and translated locally.

1. `ShowActionLabelPresentationEvent.Text`: ~37 call sites, including all `GuidanceSentence` output.
2. `ShowCommanderMessagePresentationEvent.Text`: tutorial messages.
3. `ShowBulletinPresentationEvent.Label` / `BulletinText`: mutator bulletins.
4. `InputRequest` text fields:
   - `TriggerSummaryText`, `TriggerCauseText`, `TriggerBulletinLabel` / `TriggerBulletinText`, `PassCostText`
   - handler `Title` / `ModalTitle`, option `Title` / `Text`, `FanVerb` / `FanPurpose`
5. `ScorePointsChangeEvent` VP reasons (`VPEntry.Reason`).
6. `GameResult.EndReason`, in the `BeginEndGame` RPC.
7. `RejoinService.Refuse` reasons, in the `HelloReply` RPC.
8. Player names baked in through `WithPlayer()`. This is the host's view of names, including "(you)".

Choke points where one change covers many strings:
- `CardStep.GuidanceSentence` / `WithGuidance`
- `ShowActionLabelPresentationEvent` and its DTO
- the `InputRequest` handler constructors
- `VPEntry`
- `GameMessage.SummaryText()`
- the name lookups (`FactionDisplay`, `CountryState.Label`, `CardTypeDisplay`, `TurnStepText`)

## Suggested order

1. **Remaining menu screens** (§1). Same pattern as the main menu, no design work.
2. **Name lookups** (§7), plus a key-and-arguments message format for the network (§Network). This is the foundation everything in-game builds on.
3. **In-game HUD and dialogs** (§2).
4. **History and action labels** (§4), then **card prompts** (§3), moved onto the key-and-arguments format.
5. **Tutorials** (§6) and **scenarios** (§8).
6. **Card names and texts** (§5). Mostly translation effort; the code change is a small lookup.
7. **Card art** (§10). Needs an art decision.
