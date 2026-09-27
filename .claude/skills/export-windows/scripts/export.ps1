# Release-exports the "Windows Desktop" preset into an emptied Builds/ and checks the output is shippable.
# Afterwards restores the GodotSteam DLL that headless Godot runs delete (see headless-shader-check memory).
param([string]$Godot = $env:GODOT)
$Root   = (Resolve-Path "$PSScriptRoot\..\..\..\..").Path
$builds = Join-Path $Root 'Builds'
$log    = Join-Path $env:TEMP 'qg-release-export.log'
if (-not $Godot) { $Godot = 'C:\Users\Huggles\Desktop\Godot\Godot_v4.7.1-stable_mono_win64_console.exe' }

if (Test-Path $builds) { Get-ChildItem $builds -Force | Remove-Item -Recurse -Force }
else { New-Item -ItemType Directory $builds | Out-Null }

& $Godot --headless --path $Root --export-release "Windows Desktop" "$builds\Quartermaster General.exe" *> $log
$godotExit = $LASTEXITCODE

# Some ~*.TMP files there are tracked, so only git clean (untracked) may remove leftovers.
git -C $Root checkout -- addons/godotsteam/win64/
git -C $Root clean -fq -- addons/godotsteam/win64/

$missing = @('Quartermaster General.exe', 'Quartermaster General.pck',
             'libgodotsteam.windows.template_release.x86_64.dll', 'steam_api64.dll') |
           Where-Object { -not (Test-Path (Join-Path $builds $_)) }
$debugDll = Test-Path (Join-Path $builds 'libgodotsteam.windows.template_debug.x86_64.dll')
if ($godotExit -ne 0 -or $missing -or $debugDll) {
    Get-Content $log -Tail 40
    Write-Output "EXPORT FAILED: godot exit $godotExit; missing: $($missing -join ', '); debug dll present: $debugDll. Full log: $log"
    exit 1
}

$sizeMb = [math]::Round(((Get-ChildItem $builds -Recurse -File | Measure-Object Length -Sum).Sum / 1MB), 1)
Write-Output "Export OK: $builds ($sizeMb MB). Log: $log"
