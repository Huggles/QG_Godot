# Posts a release note (markdown file) to the beta Discord channel via $env:QG_DISCORD_WEBHOOK.
param([Parameter(Mandatory)][string]$File)
$ErrorActionPreference = 'Stop'
if (-not $env:QG_DISCORD_WEBHOOK) { Write-Output 'QG_DISCORD_WEBHOOK is not set.'; exit 1 }
$text = [IO.File]::ReadAllText((Resolve-Path $File)).Trim()
if ($text.Length -gt 2000) { Write-Output "Note is $($text.Length) chars; Discord allows 2000. Shorten it."; exit 1 }

[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
# allowed_mentions empty: a stray @everyone in a commit-derived note must not ping the server.
$body = @{ content = $text; allowed_mentions = @{ parse = @() } } | ConvertTo-Json -Depth 4
$msg = Invoke-RestMethod -Uri "$($env:QG_DISCORD_WEBHOOK)?wait=true" -Method Post `
       -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($body))
Write-Output "Discord post OK: message $($msg.id) in channel $($msg.channel_id)"
