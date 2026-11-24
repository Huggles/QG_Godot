# Localization glossary: fixed game terms

This glossary lists every fixed name and term the game shows, with the **agreed** Dutch translation (decided 2026-11-24). These are the `.po` entries, and every later translation (prompts, card texts, tutorials) must use exactly these words.

In code, use `GameTerms` (`scripts/C#/Utils/GameTerms.cs`): `FactionName`, `FactionArmyLabel` / `FactionNavyLabel` / `FactionAirForceLabel`, `FactionDiscardPileLabel`, `CountryName`, `UnitName`, `CardTypeName`, `TurnStepName` / `TurnStepRule`, `TeamName`. Each returns the player's language and falls back to the English.

Proposed keys:

| What | Key pattern |
|---|---|
| Countries | `COUNTRY_<UniqueName>` |
| Factions | `FACTION_<UniqueName>` |
| Faction adjectives | `FACTION_<UniqueName>_ADJ` |
| Teams | `TEAM_AXIS`, `TEAM_ALLIES` |
| Units | `UNIT_ARMY`, `UNIT_NAVY`, `UNIT_AIR_FORCE` (+ `_PLURAL`) |
| Card types | `CARDTYPE_<CardType>` |
| Turn steps | `TURNSTEP_<TurnStep>_NAME`, `TURNSTEP_<TurnStep>_RULE` |
| Card names | `CARD_<UniqueName>_NAME` |

The data files keep their English, and the translation is display-only: the CLI, AI tooling and tutorial scripts keep matching on the English names.


## 1. Factions and teams

Sources: `QGData_Factions_V2.json`, `expansions/total_war/data/factions.json`, `FactionData.FactionAdjactiveLabels`.

| ID | English | Dutch name | English adjective | Dutch adj. (het-word / de-word) |
|---|---|---|---|---|
| GERMANY | Germany | Duitsland | German | Duits / Duitse |
| UNITED_KINGDOM | United Kingdom | Verenigd Koninkrijk | United Kingdom | Brits / Britse |
| JAPAN | Japan | Japan | Japanese | Japans / Japanse |
| SOVIET | Soviet Union | Sovjet-Unie | Soviet | Sovjet- (compound: Sovjetleger, Sovjetvloot) |
| ITALY | Italy | Italië | Italian | Italiaans / Italiaanse |
| UNITED_STATES | United States | Verenigde Staten | United States | Amerikaans / Amerikaanse |
| FRANCE (Total War) | France | Frankrijk | French | Frans / Franse |
| CHINA (Total War) | China | China | Chinese | Chinees / Chinese |

| English | Dutch |
|---|---|
| Axis | As |
| Allies | Geallieerden |
| Allied (adj.) | geallieerd / geallieerde |
| Axis (adj., "Axis Army") | van de As (e.g. "een leger van de As") |

**Adjectives are never glued to a noun in code.** Dutch adjectives change form ("Duits leger", but "Duitse vloot"). So each faction + noun combination is its own key (`FACTION_GERMANY_ARMY` → "Duits leger"), read through `GameTerms.FactionArmyLabel(faction)` and its siblings. They cover Army, Navy, Air Force, Discard Pile, Status Cards and Response Cards.

## 2. Countries (53)

Source: `QGData_Countries_V2.json`.

| ID | English | Dutch | Note |
|---|---|---|---|
| AFRICA | Africa | Afrika | |
| AUSTRALIA | Australia | Australië | |
| BALKANS | Balkans | Balkan | |
| CANADA | Canada | Canada | |
| CHINA | China | China | |
| EASTERN_EUROPE | Eastern Europe | Oost-Europa | |
| GERMANY | Germany | Duitsland | |
| HAWAII | Hawaii | Hawaï | |
| ICELAND | Iceland | IJsland | |
| INDIA | India | India | |
| INDONESIA | Indonesia | Indonesië | |
| ITALY | Italy | Italië | |
| IWO_JIMA | Iwo Jima | Iwo Jima | |
| JAPAN | Japan | Japan | |
| KAZAKHSTAN | Kazakhstan | Kazachstan | |
| LATIN_AMERICA | Latin America | Latijns-Amerika | |
| MADAGASCAR | Madagascar | Madagaskar | |
| MIDDLE_EAST | Middle East | Midden-Oosten | |
| MONGOLIA | Mongolia | Mongolië | |
| MOSCOW | Moscow | Moskou | |
| NEW_GUINEA | New Guinea | Nieuw-Guinea | |
| NEW_ZEALAND | New Zealand | Nieuw-Zeeland | |
| NORTH_AFRICA | North Africa | Noord-Afrika | |
| PACIFIC_NORTH_WEST | Pacific North West | Pacifisch Noordwesten | the region's usual Dutch description |
| PHILIPPINES | Philippines | Filipijnen | |
| RUSSIA | Russia | Rusland | |
| SCANDINAVIA | Scandinavia | Scandinavië | |
| SIBERIA | Siberia | Siberië | |
| SOUTH_EAST_ASIA | South East Asia | Zuidoost-Azië | |
| SZECHUAN | Szechuan | Sichuan | the current Dutch spelling |
| UKRAINE | Ukraine | Oekraïne | |
| UNITED_KINGDOM | United Kingdom | Verenigd Koninkrijk | |
| US_EAST | US East | Oost-VS | |
| US_WEST | US West | West-VS | |
| VLADIVOSTOK | Vladivostok | Vladivostok | |
| WESTERN_EUROPE | Western Europe | West-Europa | |
| BALTIC_SEA | Baltic Sea | Oostzee | |
| BAY_OF_BENGAL | Bay Of Bengal | Golf van Bengalen | |
| BLACK_SEA | Black Sea | Zwarte Zee | |
| CASPIAN_SEA | Caspian Sea | Kaspische Zee | |
| CENTRAL_PACIFIC | Central Pacific | Centrale Stille Oceaan | |
| EAST_PACIFIC | East Pacific | Oostelijke Stille Oceaan | |
| INDIAN_OCEAN | Indian Ocean | Indische Oceaan | |
| MEDITERRANEAN_SEA | Mediterranean Sea | Middellandse Zee | |
| NORTH_ATLANTIC | North Atlantic | Noord-Atlantische Oceaan | |
| NORTH_PACIFIC | North Pacific | Noordelijke Stille Oceaan | |
| NORTH_SEA | North Sea | Noordzee | |
| SEA_OF_JAPAN | Sea Of Japan | Japanse Zee | |
| SOUTH_ATLANTIC | South Atlantic | Zuid-Atlantische Oceaan | |
| SOUTH_CHINA_SEA | South China Sea | Zuid-Chinese Zee | |
| SOUTH_EAST_PACIFIC | South East Pacific | Zuidoostelijke Stille Oceaan | |
| SOUTHERN_OCEAN | Southern Ocean | Zuidelijke Oceaan | |
| SOUTH_PACIFIC | South Pacific | Zuidelijke Stille Oceaan | |

Ocean names use the full Dutch names ("Stille Oceaan", "Atlantische Oceaan"). Check that the long ones fit on the map.

## 3. Units and pieces

| English | Dutch (sing. / plural) |
|---|---|
| Army | Leger / Legers (het-word) |
| Navy | Vloot / Vloten (de-word) |
| Air Force (Total War) | Luchtmacht / Luchtmachten (de-word) |
| unit | eenheid / eenheden |
| piece | stuk / stukken |
| reserve ("pieces in reserve") | voorraad |

## 4. Card types

Source: `CardTypeDisplay.Label()`. Note that the card art also paints these words; see the inventory, §10.

| CardType | English | Dutch |
|---|---|---|
| BUILD_ARMY | Build Army | Leger bouwen |
| BUILD_NAVY | Build Navy | Vloot bouwen |
| LAND_BATTLE | Land Battle | Landslag |
| SEA_BATTLE | Sea Battle | Zeeslag |
| EVENT | Event | Gebeurtenis |
| STATUS | Status | Status |
| RESPONSE | Response | Reactie |
| ECONOMIC_WARFARE | Economic Warfare | Economische oorlogvoering |
| AIR_POWER (Total War) | Air Power | Luchtkracht (avoids clashing with Luchtmacht = Air Force) |
| BOLSTER (Total War) | Bolster | Versterking |

"Reactie" also matches the existing "reactievenster" (reaction window). In compounds, write "Statuskaart" and "Reactiekaart".

## 5. Turn steps

Sources: `TurnStepText.cs`, and `TotalWarRules.cs` for Air.

| Step | English name | Dutch name | English rule | Dutch rule |
|---|---|---|---|---|
| PLAY_CARD | Play | Spelen | Play or discard one card from your hand. | Speel of leg één kaart uit je hand af. |
| SUPPLY | Supply | Bevoorrading | Remove your unsupplied pieces from the board. | Verwijder je onbevoorrade stukken van het bord. |
| VICTORY_POINT | Victory | Overwinning | Collect Victory Points. | Verzamel overwinningspunten. |
| DISCARD | Discard | Afleggen | Discard as many cards as you want from your hand. | Leg zoveel kaarten uit je hand af als je wilt. |
| DRAW | Draw | Trekken | Draw until your hand has seven cards. | Trek tot je zeven kaarten in je hand hebt. |
| AIR (Total War) | Air | Lucht | Deploy or marshal an Air Force, or gain air superiority. | Zet een luchtmacht in of hergroepeer er een, of verkrijg luchtoverwicht. |

## 6. Game terms

These terms come from the card rules text, counted across all 249 card texts. They are the words card texts, prompts and tutorials must use consistently.

| English | Uses | Dutch | Note |
|---|---|---|---|
| Victory Point(s) | 43 | overwinningspunt(en) | |
| VP (abbrev.) | UI | OP | already used in the UI |
| discard | 119 | afleggen | |
| battle (verb, "battle an Army") | 80 | aanvallen (imperative: "Val … aan") | the card type is "Landslag/Zeeslag" |
| space | 78 | gebied | |
| land space / sea space | 21 / 12 | landgebied / zeegebied | |
| home space | 8 | thuisgebied | |
| build | 77 | bouwen | |
| draw | 75 | trekken | |
| (draw) deck | 70 | (trek)stapel | |
| when | 65 | wanneer / als | style |
| adjacent | 59 | aangrenzend | |
| recruit | 53 | rekruteren | |
| hand | 51 | hand | |
| unit | 47 | eenheid | |
| gain (VP) | 45 | krijgen | |
| must / may | 42 / 26 | moet / mag | |
| turn | 41 | beurt | |
| beginning of (your turn / step) | 37 | aan het begin van | |
| Axis / Allied | 30 / 17 | As / geallieerd | |
| eliminate | 29 | elimineren | |
| Play step | 27 | speelstap | follows turn-step names |
| instead | 17 | in plaats daarvan | |
| supply / supplied | 14 / 14 | bevoorrading / bevoorraad | |
| deploy | 11 | inzetten | |
| immediately | 10 | onmiddellijk | |
| discard pile | 9 | aflegstapel | |
| table ("on the table") | 7 | tafel | |
| face down | 3 | gedekt | |
| shuffle | 3 | schudden | |
| marshal (Air Force, Total War) | 5 | hergroeperen | moving an Air Force that is already on the board |
| air superiority (Total War) | 5 | luchtoverwicht | |
| round | UI | ronde | |
| reaction window | UI | reactievenster | already in use |
| supply star / supply space | UI | bevoorradingsster | |

## 7. Card names (256): kept in English

**Decision: card names stay as they are, in every language.** Only the card *text* is translated. Card names are historical proper names, and keeping them also keeps them identical to the names the CLI, AI tooling and tutorial scripts match on.

The type-only cards follow the card types in §4: Build Army, Build Navy, Land Battle, Sea Battle, Air Power.

**Base game:**
- **Events (35):** Afrika Korps; Ardennes Offensive; Arsenal of Democracy; Broad Front; Division Azul; Dutch East Indies; Fleet Deployed to Pearl Harbor; Flexible Resources; Forced Conscription; Free French Allies; General Smuts Strengthens Ties to UK; General Winter; German Aid in Greece; German-Soviet Treaty of Friendship, Cooperation, and Demarcation; Guadalcanal; Guns and Butter; Increased Commonwealth Support; Italian East Africa; Italy Attacks Communists; King Peter Enthroned in Yugoslavia; Ledo and Burma Roads; Lend Lease; Mao Tse-tung; Military Dictatorships in the Balkans; Murmansk Convoy; Operation Magic; Patton Advances; Plunder; Singapore Fortified; The Autobahn; Theater Shift; Tito's Partisans; Trans-Siberian Railroad; Vasilevsky Takes Command in the Far East
- **Status (42):** Abundant Resources; Aircraft Carriers; American Volunteer Group Expands; Amphibious Landings; Anti-Communist Sentiment; Atlantic Wall; Australia Forms the Directorate of Manpower; Balkan Resources; Bias for Action; Blitzkrieg; Bravado; Conscription; Dive Bombers; Flying Fortresse; Forward Bases; Free France; Frontal Assault; Greater East Asia Co Prosperity Sphere; Guards; Imperial Designs; Impero Italiano; Jet Fighters; Lord Linlithgow Declares India to Be at War; Mackenzie King Drafts the National Resources Mobilization Act; Mare Nostrum; Occupation Of Czechoslovakia; Radar; Resistance; Rosie the Riveter; Scorched Earth; Shvernik's Evacuation Council; Stavka Forms Artillery Corps; Superior Planning; Superior Shipyards; Swedish Iron Ore; Synthetic Fuel; The Royal Navy; Unimpeded Merchant Shipping; Volksturm; Wartime Production; Wolf Packs; Women Conscripts
- **Economic Warfare (17):** B-24 Liberator; B-26 Marauder; B-29 Superfortress; Bomber Command; Decima Flottiglia MAS (Frogmen); Firestorm Bombing; Indian Ocean Patrols; Malta Submarines; Regia Marina Closes Shipping Lane; SBD Dauntless; Submarines Enforce Blockade; Submarines Lead the Battle of the Atlantic; Submarines Prey on Unprotected Shipping; Submarines Raid Murmansk Convoy; Submarines Support Pacific Islands; Submarines of the Monsoon Group; V-Weapons
- **Response (27):** ASW Tactics; Banzai Charge; Battleship Repair; China Offensive; Defense of the Motherland; Defensive Posture; Destroyer Transport; Destroyers; Enigma Code Cracked; Fall of Singapore; German Reinforcement Counterattack; Kamikaze; Kwantung Army; Leningrad; Loyal to the Crown; Mobile Force; Monte Cassino; Moscow; RAF; Rasputitsa; Rationing; Romanian Reinforcements; Skilled Pilots; Special Naval Landing Forces; Stalingrad; Surprise Attack; Truk

**Total War:**
- **Bolster (50):** 12.8cm Flakzwilling 40; A Great, Sleeping Giant; Admiral Graf Spee; Armée de l'Air; Asian Reserves; BETASOM; Battle of Moscow; Bletchley Park; British Colonial Forces; Carpet Bombing; Cavalry Corps; Cloud Cover; Code of Bushido; Eagle Squadrons; Fallschirmjäger; Flying Tigers; Fortress Rebuilt; G7e Torpedo; Golden Square Coup; Imperial Japanese Navy; Island Fortresses; Italy Completes Carriers; Ju-52's Airlift Supplies; Ju-87; Keep Calm and Carry On; Maginot Line; Mitsubishi J8M; Nagumo Commands the First Air Fleet; Navajo Code Talkers; Operation Weserübung; Operation Zet; Our Philosophy of Government; P-51 Mustang; Pacific Bases; Pact of Steel; Pappy Boyington; Paratroops; Rangers; Regia Aeronautica; Somaliland; Special Air Service Raids Airfields; Supermarina; Tactical Innovations; The Blitz; The Pacific Empire; Torai; Vitebsk Gate; Warsaw Uprising; Yamamoto aboard the Yamato; Zhukov Drives West
- **Economic Warfare (11):** Arkhangelsk Falls; Bombing of Chongqing; Doolittle's Bombers; Elektroboot Submarines; Fu-Go Balloon Bombs; Japanese Fleet Blockades Vladivostok; Oil Embargo; Operation Lobster; Operation Pike; Stalemate in China; United States Submarines Patrol Japanese Shipping Lanes
- **Status (25):** Absolute National Defense Sphere; American Volunteer Group Expands; Anti-Japanese Volunteer Armies; Artificial Harbors; Attrition; Free France; Government Evacuates to Kuibyshev; Government in Exile; Hobart's Funnies; Hokushin-ron; Irredentismo Italiano; Japanese Mandate for the South Seas Islands; Landkreuzers; People's Liberation Army; Polish Sovereignty; Quantity has a Quality All Its Own; SHAEF; Senegalese Tirailleurs; Spanish Troops Seize Gibraltar; Tank Desant; Turkey Opens the Straits; USMC; Victory Gardens; War Bonds; Westwall
- **Events (30):** American Public Sympathizes with Chinese People; Armée de Terre; Battles of Khalkhin Gol; Cash and Carry Replaces Neutrality Acts; Dutch East Indies; Fall Weiss; Foreign Legion; France Combattante; Free Forces of the Low Countries; Free French Allies; Giuliani, Cappellini, and Torelli; Kuril Islands Landing Operation; La Royale; Ledo and Burma Roads; Les Forces Navales Françaises Libres; Operation Herkules; Polskie Państwo Podziemne; Production Initiative; Reza Shah supports Axis; Rhin et Danube; Sea Lion; Shoot First and Argue Afterwards; Siberian Transfer; Spain; Strategic Planning; Sweden Supports Finland; The Brazilian Expeditionary Force; The Hundred Regiments Offensive; Turkey Joins Axis; Wafdist Uprising
- **Response (15):** Alpini; Chinese Civil War; Endless Expanses; French Patriots Join Allies; Heavy Tanks; Imperial Manchukuo Army; Japan Arms Indian Nationalists; Kantai Kessen; Marco Polo Bridge Incident; Operation Unthinkable; Retreat and Regroup; SNLF Paratroopers; Submarines Support Fleet; Thailand; Élan

## 8. English inconsistencies found along the way

These are worth fixing in the English source before translating:

- Card texts say "Southeast Asia"; the country label is "South East Asia".
- Card texts say "Mediterranean"; the country label is "Mediterranean Sea".
- Capitalisation: "Bay Of Bengal", "Sea Of Japan", "Occupation Of Czechoslovakia".
- Typos and spacing in card names:
  - "Flying Fortresse" should be "Flying Fortress(es)".
  - "Volksturm" should be "Volkssturm".
  - "General Winter " has a trailing space.
  - "Greater East Asia Co Prosperity Sphere" should be "Co-Prosperity".
- `FactionAdjactiveLabels` uses "United Kingdom" and "United States" as adjectives ("United Kingdom Army"), consistent with the card texts. Dutch should still use "Brits" and "Amerikaans". The code name also has a typo, "Adjactive".
