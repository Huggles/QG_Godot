# Quartermaster General - Launch Two Clients for Multiplayer Testing
# This script builds the project and launches two instances

Write-Host "Building project..." -ForegroundColor Cyan
dotnet build

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed! Exiting..." -ForegroundColor Red
    exit 1
}

Write-Host "Build successful!" -ForegroundColor Green
Write-Host ""
Write-Host "Launching two game instances for multiplayer testing..." -ForegroundColor Cyan
Write-Host "Instance 1: Host - Click 'Host Game' button" -ForegroundColor Yellow
Write-Host "Instance 2: Client - Click 'Join Game' button" -ForegroundColor Yellow
Write-Host ""

# Find Godot executable (check common locations)
$godotPaths = @(
    "C:\Program Files\Godot\Godot_v4.3-stable_mono_win64.exe",
    "C:\Program Files\Godot\Godot_v4.2-stable_mono_win64.exe",
    "C:\Godot\Godot_v4.3-stable_mono_win64.exe",
    "C:\Godot\Godot_v4.2-stable_mono_win64.exe",
    "$env:LOCALAPPDATA\Godot\Godot_v4.3-stable_mono_win64.exe"
)

$godotExe = $null
foreach ($path in $godotPaths) {
    if (Test-Path $path) {
        $godotExe = $path
        break
    }
}

# If not found in common locations, try to find in PATH
if ($null -eq $godotExe) {
    $godotInPath = Get-Command godot -ErrorAction SilentlyContinue
    if ($godotInPath) {
        $godotExe = $godotInPath.Source
    }
}

if ($null -eq $godotExe) {
    Write-Host "Godot executable not found!" -ForegroundColor Red
    Write-Host "Please specify the path to your Godot executable:" -ForegroundColor Yellow
    $godotExe = Read-Host "Godot path"
    
    if (-not (Test-Path $godotExe)) {
        Write-Host "Invalid path! Exiting..." -ForegroundColor Red
        exit 1
    }
}

Write-Host "Using Godot at: $godotExe" -ForegroundColor Green
Write-Host ""

# Get the current project directory
$projectPath = Get-Location

# Launch two instances
Write-Host "Launching Instance 1 (Host)..." -ForegroundColor Cyan
Start-Process -FilePath $godotExe -ArgumentList "--path", "`"$projectPath`"" -WindowStyle Normal

# Wait a bit before launching second instance
Start-Sleep -Seconds 2

Write-Host "Launching Instance 2 (Client)..." -ForegroundColor Cyan
Start-Process -FilePath $godotExe -ArgumentList "--path", "`"$projectPath`"" -WindowStyle Normal

Write-Host ""
Write-Host "Both instances launched!" -ForegroundColor Green
Write-Host "Remember:" -ForegroundColor Yellow
Write-Host "  1. In Instance 1: Click 'Host Game'" -ForegroundColor White
Write-Host "  2. In Instance 2: Click 'Join Game'" -ForegroundColor White
Write-Host "  3. In Instance 1 (Host): Click 'Start Game' when ready" -ForegroundColor White
