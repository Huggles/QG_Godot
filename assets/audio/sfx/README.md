# Sound effects

Drop sound effects here (`.ogg`, `.wav` or `.mp3`). `AudioManager` scans this folder
recursively on startup and keys every clip by its **filename without extension**, so
`card_flip.ogg` becomes:

```csharp
AudioManager.PlaySfx("card_flip");
```

Any number of effects can overlap. Names are case-insensitive and must be unique
across subfolders.
