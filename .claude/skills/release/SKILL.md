---
name: release
description: Ship a new beta release of Quartermaster General from the MacBook — bump the patch version, release-export the Windows and macOS builds, write player-facing release notes, upload both depots to Steam as one build (live on default) and post the notes to the beta Discord. Run only when the user types /release.
disable-model-invocation: true
---

# /release

This runs on the MacBook, which is the only machine that uploads to Steam. Both platforms go up as one Steam build, because a branch can have only one build live at a time. A build that contains only one depot would take the other platform off `default`. The Windows PC has `/export-windows` for local test builds only.

Run everything from the repo root with Bash. The scripts live in `.claude/skills/release/scripts/`.
Nothing public happens before step 6. Stop at the first failure and report the script's output verbatim.

## 1. Preflight
Stop and tell the user what is missing if any of these fail:
- `git branch --show-current` is `master`, and `git pull --ff-only` succeeds.
- `git status --porcelain` is empty. **Resume case:** if the only changes are `export_presets.cfg` and `project.godot`, an earlier run was interrupted after its bump. Continue, and let step 2 reuse that version.
- `dotnet build "Quartermaster General.csproj" -v q --nologo` exits 0.
- `/Applications/Godot_mono.app` exists (or `$GODOT` points to a Godot 4.7.1 mono binary), and `~/Library/Application Support/Godot/export_templates/4.7.1.stable.mono/` contains `macos.zip` and `windows_release_x86_64.exe`. If the Windows template is missing, extract the `templates/windows_*_x86_64*.exe` files from the official `Godot_v4.7.1-stable_mono_export_templates.tpz` on the Godot GitHub releases page into that folder.
- `~/Steam/steamcmd.sh` exists, or `$STEAMCMD` points to one. `arch -x86_64 /usr/bin/true` exits 0: `steamcmd` is Intel-only and needs Rosetta (`softwareupdate --install-rosetta --agree-to-license`).
- `STEAM_BUILD_USER` and `QG_DISCORD_WEBHOOK` are set in the environment. If not, the user adds `export NAME=value` lines for them to `~/.zshrc`.

## 2. Bump the version
`.claude/skills/release/scripts/bump-version.sh` prints the new version, `X.Y.Z`. It reads `config/version` in `project.godot` and writes the new version there and into the Windows preset. Leave the files uncommitted for now.

## 3. Export
`.claude/skills/release/scripts/export.sh X.Y.Z`. This takes about a minute and empties `Builds/windows/` and `Builds/macOS/` first. It fails loudly if an exe, pck, .NET data folder or release GodotSteam library is missing, if a debug GodotSteam library slipped in, or if the macOS bundle's version is not `X.Y.Z`.
The Windows exe is built without `rcedit`, so it has Godot's default icon and file details. That is expected.

## 4. Draft the release note
- Range: `git describe --tags --abbrev=0 --match "v*"`..HEAD. **If there is no tag**, start from the newest `Release v…` commit (`git log --grep "^Release v" -1 --format=%h`). If there is none of either, ask the user which commit to start from.
- Read `git log --format="%h %s%n%b" <range>`. For vague subjects like "Fix internals", look at `git show --stat` or the diff to work out what changed for players.
- Write it for beta testers who know the board game but not the code. Rules:
  - Group bullets under New, Changed and Fixed. Leave out empty sections.
  - One line per bullet, describing the effect in game ("Leaving a game mid-turn no longer freezes the lobby"), not the implementation.
  - Leave out purely internal work: refactors, tests, tooling, the sim harness, docs, `.uid` files, export presets.
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
Show the user the version, both export sizes and the full note. Say plainly: **"This uploads the Windows and macOS builds to Steam as one build and sets it live on the default branch, then posts the note to the beta Discord."** Wait for an explicit go-ahead. Apply any edits they ask for to the note file.
If they abort: run `git checkout -- export_presets.cfg project.godot` and delete the note file.

## 6. Upload to Steam
`.claude/skills/release/scripts/upload-steam.sh X.Y.Z`
- Exit 1 means the upload failed. Common causes are an expired cached login or a Steam Guard prompt. In that case, tell the user to run `~/Steam/steamcmd.sh +login <user> +quit` in their own terminal, then rerun `/release`. It resumes at the same version.
- Exit 2 means the build uploaded but Steam refused to set it live on default. Carry on with the release, and pass the user the Steamworks builds link the script prints.

## 7. Post to Discord
`.claude/skills/release/scripts/post-discord.sh releases/vX.Y.Z.md`

## 8. Commit and tag
Stage `export_presets.cfg`, `project.godot` and `releases/vX.Y.Z.md`. Commit as `Release vX.Y.Z`, then `git tag vX.Y.Z`. **Do not push.** Finish with a short summary: the version, the Steam BuildID, whether it went live, and the Discord post. Also tell the user that `git push --follow-tags` is left to do.
