#!/usr/bin/env bash
# Posts a release note (markdown file) to the beta Discord channel via $QG_DISCORD_WEBHOOK.
# Usage: post-discord.sh releases/vX.Y.Z.md
set -eu
FILE="${1:?usage: post-discord.sh <note.md>}"
[ -n "${QG_DISCORD_WEBHOOK:-}" ] || { echo 'QG_DISCORD_WEBHOOK is not set.'; exit 1; }

# allowed_mentions empty: a stray @everyone in a commit-derived note must not ping the server.
body="$(jq -Rs '{content: (. | sub("^\\s+"; "") | sub("\\s+$"; "")), allowed_mentions: {parse: []}}' "$FILE")"
len="$(jq -r '.content | length' <<< "$body")"
[ "$len" -le 2000 ] || { echo "Note is $len chars; Discord allows 2000. Shorten it."; exit 1; }

msg="$(curl -sS --fail-with-body -X POST "$QG_DISCORD_WEBHOOK?wait=true" \
       -H 'Content-Type: application/json; charset=utf-8' --data-binary "$body")" \
    || { echo "DISCORD POST FAILED: $msg"; exit 1; }
echo "Discord post OK: message $(jq -r .id <<< "$msg") in channel $(jq -r .channel_id <<< "$msg")"
