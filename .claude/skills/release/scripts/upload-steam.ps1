# Uploads Builds/ to Steam with the repo's steamcmd, using its cached login for $env:STEAM_BUILD_USER.
# -SetLive '' uploads without setting any branch live (for testing).
param([Parameter(Mandatory)][string]$Version, [string]$SetLive = 'default')
$Root     = (Resolve-Path "$PSScriptRoot\..\..\..\..").Path
$steamcmd = Join-Path $Root 'steamcmd\steamcmd.exe'
$work     = Join-Path $Root 'steamcmd\build'
if (-not $env:STEAM_BUILD_USER) { Write-Output 'STEAM_BUILD_USER is not set.'; exit 1 }

New-Item -ItemType Directory -Force (Join-Path $work 'output') | Out-Null
$vdf = Join-Path $work 'app_build_5159090.vdf'
$text = [IO.File]::ReadAllText((Join-Path $PSScriptRoot '..\steam\app_build.vdf'))
$text = $text.Replace('{{DESC}}', "v$Version").Replace('{{CONTENT_ROOT}}', (Join-Path $Root 'Builds')).
              Replace('{{BUILD_OUTPUT}}', (Join-Path $work 'output')).Replace('{{SET_LIVE}}', $SetLive)
[IO.File]::WriteAllText($vdf, $text, (New-Object System.Text.UTF8Encoding $false))

# NoPromptForPassword makes a missing cached login fail fast instead of waiting on a prompt nobody sees.
$out = & $steamcmd '+@NoPromptForPassword' 1 +login $env:STEAM_BUILD_USER +run_app_build $vdf +quit 2>&1 | ForEach-Object { "$_" }
$out | Where-Object { $_ -match 'ERROR|Fail|Success|BuildID|Logged in|password|Steam Guard' }

$done = $out | Select-String 'Successfully finished AppID \d+ build \(BuildID (\d+)\)'
if (-not $done) { Write-Output "STEAM UPLOAD FAILED. Full steamcmd output is in $work\output."; exit 1 }
Write-Output "Steam upload OK: BuildID $($done.Matches[0].Groups[1].Value)"
if ($SetLive -and ($out -match 'set.*live|SetLive' | Where-Object { $_ -match 'ERROR|Fail|denied|not allowed' })) {
    Write-Output "SETLIVE REFUSED: set the build live by hand at https://partner.steamgames.com/apps/builds/5159090"
    exit 2
}
