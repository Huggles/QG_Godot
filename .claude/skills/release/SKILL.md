---
name: release
description: Ship a new beta release of Quartermaster General — bump the patch version, release-export the Windows build, write player-facing release notes, upload to Steam (live on default) and post the notes to the beta Discord. Run only when the user types /release.
disable-model-invocation: true
---

# /release

Run everything from the repo root with the PowerShell tool. The scripts live in `.claude/skills/release/scripts/`.
Nothing public happens before step 5. Stop at the first failure and report the script's output verbatim.

## 1. Preflight
Stop and tell the user what is missing if any of these fail:
- `git branch --show-current` is `master`.
- `git status --porcelain` is empty. **Resume case:** if the only changes are `export_presets.cfg` and `project.godot`, an earlier run was interrupted after its bump. Continue, and let step 2 reuse that version.
- `dotnet build "Quartermaster General.csproj" -v q --nologo` exits 0.
- `steamcmd\steamcmd.exe` exists.
- The user env vars `QG_DISCORD_WEBHOOK` and `STEAM_BUILD_USER` are set. Read them with `[Environment]::GetEnvironmentVariable('<name>','User')`, because a shell started before `setx` won't have them. Pass them into the scripts' environment.

## 2. Bump the version
`.\.claude\skills\release\scripts\bump-version.ps1` prints the new version, `X.Y.Z`. Leave the files uncommitted for now.

## 3. Export
`.\.claude\skills\release\scripts\export.ps1`. This takes a few minutes and empties `Builds/` first. It fails loudly if the exe, the pck or the release GodotSteam DLL is missing.

## 4. Draft the release note
- Range: `git describe --tags --abbrev=0 --match "v*"`..HEAD. **If there is no tag yet**, ask the user which commit to start from.
- Read `git log --format="%h %s%n%b" <range>`. For vague subjects like "Fix internals", look at `git show --stat` or the diff to work out what changed for players.
- Write it for beta testers who know the board game but not the code. Rules:
  - Group bullets under New, Changed and Fixed. Leave out empty sections.
  - One line per bullet, describing the effect in game ("Leaving a game mid-turn no longer freezes the lobby"), not the implementation.
  - Leave out purely internal work: refactors, tests, tooling, the sim harness, docs.
  - No commit hashes, no file names. Keep it under 2000 characters, Discord's limit.
- Format:
  ```
  **Quartermaster General WW2 — vX.Y.Z** (beta)

  **New**
  - …

  **Fixed**
  - …
  ```
- Save it to `releases/vX.Y.Z.md`.

## 5. Confirmation gate — the only stop
Show the user the version, the export size and the full note. Say plainly: **"This uploads to Steam and sets it live on the default branch, then posts the note to the beta Discord."** Wait for an explicit go-ahead. Apply any edits they ask for to the note file.
If they abort: run `git checkout -- export_presets.cfg project.godot` and delete the note file.

## 6. Upload to Steam
`.\.claude\skills\release\scripts\upload-steam.ps1 -Version X.Y.Z`
- Exit 1 means the upload failed. Common causes: an expired cached login, or a Steam Guard prompt. In that case, tell the user to run `steamcmd\steamcmd.exe +login <user> +quit` in their own terminal, then rerun `/release`. It resumes at the same version.
- Exit 2 means the build uploaded but Steam refused to set it live on default. Carry on with the release, and pass the user the Steamworks builds link the script prints.

## 7. Post to Discord
`.\.claude\skills\release\scripts\post-discord.ps1 -File releases\vX.Y.Z.md`

## 8. Commit and tag
Stage `export_presets.cfg`, `project.godot` and `releases/vX.Y.Z.md`. Commit as `Release vX.Y.Z`, then `git tag vX.Y.Z`. **Do not push.** Finish with a short summary: the version, the Steam BuildID, whether it went live, and the Discord post. Also tell the user that `git push --follow-tags` is left to do.
