# Music

Drop music tracks here (`.ogg`, `.wav` or `.mp3`). `AudioManager` scans this folder
recursively on startup and keys every clip by its **path below this folder, without the
extension**, so `main_theme.ogg` becomes:

```csharp
AudioManager.PlayMusic("main_theme");
```

and `germany/march.ogg` becomes `AudioManager.PlayMusic("germany/march")`. Names are
case-insensitive; because the folder is part of the name, two subfolders may hold a file
with the same name.

Only one music track plays at a time — `PlayMusic` replaces whatever is running.

## Folders

| Folder | What it holds |
| --- | --- |
| *(root)* | `MainMenuMusic`, the menu theme. Never part of the in-game playlist. |
| `game/` | General in-game music, belonging to no particular faction. |
| `<faction>/` | That faction's music. |

Faction folders are named after the `Faction` enum lowercased — `germany`, `italy`, `japan`,
`soviet`, `united_kingdom`, `united_states` — the same convention `FactionData.factionName`
uses for textures. A faction with no folder, or an empty one, simply contributes nothing.

`GameMusic.StartForLocalPlayer` builds the in-game playlist from `game/` plus every faction
folder, shuffles it, and moves a track from one of the local player's own factions to the
front. Adding a track is therefore just a matter of dropping the file in the right folder.
