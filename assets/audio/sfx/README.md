# Sound effects

Drop sound effects here (`.ogg`, `.wav` or `.mp3`). `AudioManager` scans this folder
recursively on startup and keys every clip by its **filename without extension**, so
`card_flip.ogg` becomes:

```csharp
AudioManager.PlaySfx("card_flip");
```

Any number of effects can overlap. Names are case-insensitive and must be unique
across subfolders.

## settings.json — player-configurable cues

`settings.json` declares the cues the player can choose the sound for. Each entry has a
stable `name` (the key the choice is saved under, and the key code uses), a display
`label`, and the `sound_files` on offer — each with its own `name`, `label`, and `file`
path with the extension omitted.

Game code raises a cue by its setting name, never by filename:

```csharp
AudioManager.PlaySfxSetting("inputRequest");
```

Adding an entry here adds a row to the Audio tab of the settings menu (reachable from the
main menu, and in-game through Escape) with no code or scene change.
A cue the player has never chosen for uses the first sound listed. Options whose `file`
is not present in this folder are dropped at startup with an error in the console, so a
typo shows up once on boot rather than as a cue that silently does nothing.
