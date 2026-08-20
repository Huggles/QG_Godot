# Music

Drop music tracks here (`.ogg`, `.wav` or `.mp3`). `AudioManager` scans this folder
recursively on startup and keys every clip by its **filename without extension**, so
`main_theme.ogg` becomes:

```csharp
AudioManager.PlayMusic("main_theme");
```

Only one music track plays at a time — `PlayMusic` replaces whatever is running.
Names are case-insensitive and must be unique across subfolders.
