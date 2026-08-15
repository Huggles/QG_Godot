# Quick Launch Six - One ENet host + five auto-joining clients, all waiting in the lobby
# Optimized for running from VSCode task (F9)
#
# The host uses the same lobby-only debug path as F7: it auto-hosts on port 7777 but never
# auto-starts, so factions are picked by hand and the host clicks "Start Game" when ready.
# The clients use auto_join=true, which skips the main menu and connects to 127.0.0.1.
#
# Usage: .\quick_launch_six.ps1 [-Clients 5]

param(
    [int]$Clients = 5
)

# MultiplayerLobby hosts with ENetMultiplayerPeer.CreateServer(port, 6): six client slots.
if ($Clients -lt 1 -or $Clients -gt 6) {
    Write-Host "Clients must be between 1 and 6 (the host only opens 6 client slots)." -ForegroundColor Red
    exit 1
}

Write-Host "Building project..." -ForegroundColor Cyan
dotnet build

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

$total = $Clients + 1
Write-Host "Build successful! Launching 1 host + $Clients client(s)..." -ForegroundColor Green

# Try common Godot locations
$godot = $null
$attempts = @(
    "godot",
    "C:\Users\Huggles\Desktop\Godot\Godot_v4.7.1-stable_mono_win64_console.exe"
)

foreach ($path in $attempts) {
    try {
        if (Get-Command $path -ErrorAction SilentlyContinue) {
            $godot = $path
            break
        }
    } catch { }
}

if (-not $godot) {
    foreach ($path in $attempts[1..($attempts.Length-1)]) {
        if (Test-Path $path) {
            $godot = $path
            break
        }
    }
}

if (-not $godot) {
    Write-Host ""
    Write-Host "Godot executable not found!" -ForegroundColor Red
    Write-Host "Please add Godot to your PATH or edit quick_launch_six.ps1" -ForegroundColor Yellow
    Write-Host "Expected locations checked:" -ForegroundColor Gray
    foreach ($path in $attempts) {
        Write-Host "  - $path" -ForegroundColor DarkGray
    }
    exit 1
}

Write-Host "Found Godot: $godot" -ForegroundColor Gray
Write-Host ""

$cwd = Get-Location

# ── Window tiling ─────────────────────────────────────────────────────────────
# Six overlapping 1920x1080 windows are unusable, so lay them out in a 3x2 grid on the
# primary screen. Falls back to 1920x1080 if the screen bounds cannot be read.
$screenWidth  = 1920
$screenHeight = 1080
try {
    Add-Type -AssemblyName System.Windows.Forms -ErrorAction Stop
    $bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
    $screenWidth  = [math]::Floor($bounds.Width * 1.5)
    $screenHeight = [math]::Floor($bounds.Height * 1.5)
    Write-Host "Screen size detected: ${screenWidth}x${screenHeight}" -ForegroundColor DarkGray
} catch {
    Write-Host "Could not read screen size; tiling against 1920x1080." -ForegroundColor DarkGray
}

$cols = 3
$rows = [math]::Ceiling($total / $cols)
$titleBar = 40   # leave room for the window chrome so tiles do not overlap vertically

$tileWidth  = [math]::Floor($screenWidth / $cols)
$tileHeight = [math]::Floor($screenHeight / $rows)
Write-Host "Tiling $total windows in ${cols}x${rows} grid: ${tileWidth}x${tileHeight} each" -ForegroundColor DarkGray
$winWidth   = $tileWidth - 10
$winHeight  = $tileHeight

function Get-TileArgs([int]$index) {
    $col = $index % $cols
    $row = [math]::Floor($index / $cols)
    $x   = $col * $tileWidth
    $y   = $row * $tileHeight
    return @("--resolution", "${winWidth}x${winHeight}", "--position", "$x,$y")
}

# ── Host ──────────────────────────────────────────────────────────────────────
Write-Host "Starting host (instance 1, lobby only, no auto-start)..." -ForegroundColor Yellow
$hostArgs = @("--path", $cwd) + (Get-TileArgs 0) + @("--", "instance=1", "lobby_only=true", "is_debug_multiplayer=true")
Start-Process $godot -ArgumentList $hostArgs
Start-Sleep -Seconds 3

# ── Clients ───────────────────────────────────────────────────────────────────
for ($i = 1; $i -le $Clients; $i++) {
    $instance = $i + 1
    Write-Host "Starting client $i (instance $instance, auto-join)..." -ForegroundColor Yellow
    $clientArgs = @("--path", $cwd) + (Get-TileArgs $i) + @("--", "auto_join=true", "instance=$instance")
    Start-Process $godot -ArgumentList $clientArgs
    Start-Sleep -Seconds 1
}

Write-Host ""
Write-Host "Done! $total instance(s) launched." -ForegroundColor Green
Write-Host "All clients join the host's lobby on port 7777. The game does NOT start automatically:" -ForegroundColor Cyan
Write-Host "  Assign factions in the lobby, then click 'Start Game' in the host window." -ForegroundColor White
