# VSCode Keybinding Setup for F6 (Launch Two Instances)

A VSCode task has been created for launching two game instances, but you need to add the keybinding manually since VSCode doesn't support workspace-level keybindings.

## How to Add the F6 Keybinding

1. In VSCode, press `Ctrl+Shift+P` (or `F1`)
2. Type "Preferences: Open Keyboard Shortcuts (JSON)"
3. Select it to open your `keybindings.json`
4. Add this entry to the array:

```json
{
    "key": "f6",
    "command": "workbench.action.tasks.runTask",
    "args": "Launch Two Instances (Multiplayer)"
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
    }
]
```

## Alternative: Using Command Palette

If you don't want to set up a keybinding, you can also:

1. Press `Ctrl+Shift+P` (or `F1`)
2. Type "Tasks: Run Task"
3. Select "Launch Two Instances (Multiplayer)"

## What Happens When You Press F6

1. ✅ Builds the project with `dotnet build`
2. ✅ Launches two Godot instances
3. ✅ Both open to the multiplayer lobby
4. ✅ You can test multiplayer on one machine

## Quick Reference

- **F5** - Normal run (single instance)
- **F6** - Build and launch two instances for multiplayer testing

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
