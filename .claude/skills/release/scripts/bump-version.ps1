# Bumps the patch number in the Windows export preset and project.godot, then prints the version.
# If the version files already differ from HEAD (an interrupted earlier run), prints that version unchanged.
$ErrorActionPreference = 'Stop'
$Root    = (Resolve-Path "$PSScriptRoot\..\..\..\..").Path
$preset  = Join-Path $Root 'export_presets.cfg'
$project = Join-Path $Root 'project.godot'
$utf8    = New-Object System.Text.UTF8Encoding $false

$presetText = [IO.File]::ReadAllText($preset)
if ($presetText -notmatch 'application/file_version="(\d+)\.(\d+)\.(\d+)\.\d+"') { throw "No application/file_version in $preset" }
$major = [int]$Matches[1]; $minor = [int]$Matches[2]; $patch = [int]$Matches[3]

git -C $Root diff --quiet HEAD -- export_presets.cfg project.godot
if ($LASTEXITCODE -ne 0) { Write-Output "$major.$minor.$patch"; exit 0 }

$version = "$major.$minor.$($patch + 1)"
$presetText = $presetText -replace 'application/(file|product)_version="[^"]*"', "application/`$1_version=`"$version.0`""
[IO.File]::WriteAllText($preset, $presetText, $utf8)

$projectText = [IO.File]::ReadAllText($project)
if ($projectText -notmatch 'config/version="[^"]*"') { throw "No config/version in $project" }
$projectText = $projectText -replace 'config/version="[^"]*"', "config/version=`"$version`""
[IO.File]::WriteAllText($project, $projectText, $utf8)

Write-Output $version
