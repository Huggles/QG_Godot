# Quick Launch Lobby - Both instances join each other's lobby, no auto-start
# Optimized for running from VSCode task (F7)

Write-Host "Building project..." -ForegroundColor Cyan
dotnet build

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

Write-Host "Build successful! Launching two instances (lobby only)..." -ForegroundColor Green

# Try common Godot locations
$godot = $null
$attempts = @(
    "godot",
    "C:\Users\Hugo\Downloads\Godot_v4.5-stable_mono_win64\Godot_v4.5-stable_mono_win64\Godot_v4.5-stable_mono_win64_console.exe"
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

if ($godot) {
    Write-Host "Found Godot: $godot" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Starting Instance 1 (Host, lobby only)..." -ForegroundColor Yellow
    Start-Process $godot -ArgumentList "--path", (Get-Location), "--", "instance=1", "lobby_only=true"
    Start-Sleep -Seconds 2
    Write-Host "Starting Instance 2 (Client, lobby only)..." -ForegroundColor Yellow
    Start-Process $godot -ArgumentList "--path", (Get-Location), "--", "instance=2", "lobby_only=true"
    Write-Host ""
    Write-Host "Instructions:" -ForegroundColor Cyan
    Write-Host "  Both instances will auto-host/join the lobby." -ForegroundColor White
    Write-Host "  Click 'Start Game' manually in Instance 1 when ready." -ForegroundColor White
    Write-Host ""
    Write-Host "Done! Both instances launched." -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "Godot executable not found!" -ForegroundColor Red
    Write-Host "Please add Godot to your PATH or edit quick_launch_lobby.ps1" -ForegroundColor Yellow
    Write-Host "Expected locations checked:" -ForegroundColor Gray
    foreach ($path in $attempts) {
        Write-Host "  - $path" -ForegroundColor DarkGray
    }
}
