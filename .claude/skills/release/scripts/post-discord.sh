#!/usr/bin/env bash
# Posts a release note (markdown file) to the beta Discord channel via $QG_DISCORD_WEBHOOK.
# Usage: post-discord.sh releases/vX.Y.Z.md [image]   The optional image is attached below the note.
set -eu
FILE="${1:?usage: post-discord.sh <note.md> [image]}"
IMAGE="${2:-}"
[ -z "$IMAGE" ] || [ -f "$IMAGE" ] || { echo "No image at $IMAGE"; exit 1; }
[ -n "${QG_DISCORD_WEBHOOK:-}" ] || { echo 'QG_DISCORD_WEBHOOK is not set.'; exit 1; }

# allowed_mentions empty: a stray @everyone in a commit-derived note must not ping the server.
body="$(jq -Rs '{content: (. | sub("^\\s+"; "") | sub("\\s+$"; "")), allowed_mentions: {parse: []}}' "$FILE")"
len="$(jq -r '.content | length' <<< "$body")"
[ "$len" -le 2000 ] || { echo "Note is $len chars; Discord allows 2000. Shorten it."; exit 1; }

if [ -n "$IMAGE" ]; then
    # Read the JSON from a file: inline, curl -F would split it on ';' or trip over quotes.
    payload="$(mktemp)"; trap 'rm -f "$payload"' EXIT
    printf '%s' "$body" > "$payload"
    msg="$(curl -sS --fail-with-body -X POST "$QG_DISCORD_WEBHOOK?wait=true" \
           -F "payload_json=<$payload;type=application/json" -F "files[0]=@$IMAGE;type=image/png")" \
        || { echo "DISCORD POST FAILED: $msg"; exit 1; }
else
    msg="$(curl -sS --fail-with-body -X POST "$QG_DISCORD_WEBHOOK?wait=true" \
           -H 'Content-Type: application/json; charset=utf-8' --data-binary "$body")" \
        || { echo "DISCORD POST FAILED: $msg"; exit 1; }
fi
echo "Discord post OK: message $(jq -r .id <<< "$msg") in channel $(jq -r .channel_id <<< "$msg")"
