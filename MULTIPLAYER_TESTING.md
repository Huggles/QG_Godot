# Multiplayer Testing Scripts

Scripts to quickly launch two game instances for multiplayer testing.

## Usage

### Option 1: Double-click (Easiest)
Simply double-click `launch_multiplayer.bat`

### Option 2: PowerShell
```powershell
.\launch_multiplayer.ps1
```

### Option 3: Quick Launch
```powershell
.\quick_launch.ps1
```

## What the scripts do

1. **Build** the project with `dotnet build`
2. **Launch two Godot instances** of the game
3. Each runs the MultiplayerLobby scene

## How to test multiplayer

Once both windows are open:

1. **Instance 1**: Click "Host Game"
   - This will create a server on port 7777
   
2. **Instance 2**: Click "Join Game" 
   - Make sure IP is set to `127.0.0.1` (default)
   
3. **Instance 1 (Host)**: Click "Start Game"
   - Both instances will load into the game
   - Player 1 (Host) controls AXIS factions
   - Player 2 controls ALLIES factions

## Troubleshooting

**"Godot executable not found"**
- Make sure Godot is in your PATH, or
- Edit `launch_multiplayer.ps1` and add your Godot path to `$godotPaths` array

**"Execution policy" error**
- Run PowerShell as Administrator
- Execute: `Set-ExecutionPolicy RemoteSigned`
- Or use the `.bat` file instead

## Testing on different machines

To test over network:
1. Host machine: Click "Host Game"
2. Client machine: 
   - Enter host's IP address (not 127.0.0.1)
   - Click "Join Game"
3. Both machines need to allow port 7777 through firewall
