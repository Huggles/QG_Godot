# Quick Launch - Dedicated headless server + GUI clients
# Launches one headless authoritative server (no window/rendering) and N GUI clients that auto-join.
#
# Usage: .\quick_launch_server.ps1 [-Players 2]

param(
    [int]$Players = 2
)

Write-Host "Building project..." -ForegroundColor Cyan
dotnet build

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

Write-Host "Build successful! Launching dedicated server + $Players client(s)..." -ForegroundColor Green

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
    Write-Host "Please add Godot to your PATH or edit quick_launch_server.ps1" -ForegroundColor Yellow
    foreach ($path in $attempts) {
        Write-Host "  - $path" -ForegroundColor DarkGray
    }
    exit 1
}

Write-Host "Found Godot: $godot" -ForegroundColor Gray
Write-Host ""

$cwd = Get-Location

Write-Host "Starting dedicated server (headless, waiting for $Players client(s))..." -ForegroundColor Yellow
Start-Process $godot -ArgumentList "--headless", "--path", $cwd, "--", "dedicated_server=true", "players=$Players"
Start-Sleep -Seconds 2

for ($i = 1; $i -le $Players; $i++) {
    Write-Host "Starting client $i (auto-join)..." -ForegroundColor Yellow
    Start-Process $godot -ArgumentList "--path", $cwd, "--", "auto_join=true", "instance=$i"
    Start-Sleep -Seconds 1
}

Write-Host ""
Write-Host "Done! Dedicated server + $Players client(s) launched." -ForegroundColor Green
Write-Host "The server auto-assigns factions and starts once all clients connect." -ForegroundColor White
