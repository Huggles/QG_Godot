# VSCode Keybinding Setup for F6 / F7 (Launch Two Instances)

VSCode tasks have been created for launching two game instances, but keybindings must be added manually since VSCode doesn't support workspace-level keybindings.

## How to Add the F6 and F7 Keybindings

1. In VSCode, press `Ctrl+Shift+P` (or `F1`)
2. Type "Preferences: Open Keyboard Shortcuts (JSON)"
3. Select it to open your `keybindings.json`
4. Add these entries to the array:

```json
{
    "key": "f6",
    "command": "workbench.action.tasks.runTask",
    "args": "Launch Two Instances (Multiplayer)"
},
{
    "key": "f7",
    "command": "workbench.action.tasks.runTask",
    "args": "Launch Two Instances (Lobby Only)"
},
{
    "key": "f9",
    "command": "workbench.action.tasks.runTask",
    "args": "Launch Six Instances (Lobby Only)"
}
```

### Complete Example

Your keybindings.json should look something like this:

```json
[
    // ... your existing keybindings ...
    {
        "key": "f6",
        "command": "workbench.action.tasks.runTask",
        "args": "Launch Two Instances (Multiplayer)"
    },
    {
        "key": "f7",
        "command": "workbench.action.tasks.runTask",
        "args": "Launch Two Instances (Lobby Only)"
    },
    {
        "key": "f9",
        "command": "workbench.action.tasks.runTask",
        "args": "Launch Six Instances (Lobby Only)"
    }
]
```

## Alternative: Using Command Palette

If you don't want to set up keybindings, you can also:

1. Press `Ctrl+Shift+P` (or `F1`)
2. Type "Tasks: Run Task"
3. Select "Launch Two Instances (Multiplayer)" or "Launch Two Instances (Lobby Only)"

## What Happens When You Press F6

1. ✅ Builds the project with `dotnet build`
2. ✅ Launches one **headless dedicated server** (no window) + two GUI client instances
3. ✅ Both clients auto-join the server
4. ✅ The server auto-assigns factions (one client → Axis, the other → Allies — same team split as before; swap with `swapped_teams=true`) and starts the game once both clients connect

## What Happens When You Press F7

1. ✅ Builds the project with `dotnet build`
2. ✅ Launches two Godot instances
3. ✅ Instance 1 auto-hosts, Instance 2 auto-joins
4. ✅ Both wait in the lobby — click **Start Game** manually in Instance 1 when ready

## What Happens When You Press F9

1. ✅ Builds the project with `dotnet build`
2. ✅ Launches six Godot instances, tiled in a 3x2 grid on the primary screen
3. ✅ Instance 1 auto-hosts over ENet on port 7777; instances 2-6 auto-join `127.0.0.1`
4. ✅ All six wait in the lobby — assign factions, then click **Start Game** in the host window

## Quick Reference

- **F5** - Normal run / debug (single instance)
- **F6** - Build and launch a headless dedicated server + two clients, auto-start the game
- **F7** - Build and launch two instances, stay in lobby (manual start)
- **F8** - Build and launch two plain instances at the main menu (nothing automated)
- **F9** - Build and launch six instances (1 host + 5 clients), stay in lobby (manual start)

---

## Checking if F6 is Already Used

F6 is typically unbound in VSCode by default, but you can verify:
1. Open Command Palette (`Ctrl+Shift+P`)
2. Type "Preferences: Open Keyboard Shortcuts"
3. Search for "f6" to see if it's already assigned

If F6 is already in use and you don't want to override it, you can use a different key like:
- `Ctrl+F6`
- `Shift+F6`  
- `F7`
- Any other preferred combination
