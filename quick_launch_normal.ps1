# Quick Launch (Normal) - Two plain instances, straight to the main menu
# Optimized for running from VSCode task (F8)
# No debug multiplayer, no auto host/join, no team assignments: both windows behave
# exactly like a shipped build, so hosting/joining is done by hand from the menu.

Write-Host "Building project..." -ForegroundColor Cyan
dotnet build

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

Write-Host "Build successful! Launching two instances (normal)..." -ForegroundColor Green

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

if ($godot) {
    Write-Host "Found Godot: $godot" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Starting Instance 1..." -ForegroundColor Yellow
    Start-Process $godot -ArgumentList "--path", (Get-Location)
    Start-Sleep -Seconds 2
    Write-Host "Starting Instance 2..." -ForegroundColor Yellow
    Start-Process $godot -ArgumentList "--path", (Get-Location)
    Write-Host ""
    Write-Host "Both instances are at the main menu. Nothing is automated:" -ForegroundColor Cyan
    Write-Host "  Host, join and start the game manually." -ForegroundColor White
    Write-Host ""
    Write-Host "Done! Both instances launched." -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "Godot executable not found!" -ForegroundColor Red
    Write-Host "Please add Godot to your PATH or edit quick_launch_normal.ps1" -ForegroundColor Yellow
    Write-Host "Expected locations checked:" -ForegroundColor Gray
    foreach ($path in $attempts) {
        Write-Host "  - $path" -ForegroundColor DarkGray
    }
}
