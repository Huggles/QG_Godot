---
name: export-windows
description: Release-export the Windows build of Quartermaster General on the Windows PC into Builds/, for local testing. It does not bump the version, upload to Steam or post to Discord — releases run from the MacBook with /release. Run only when the user types /export-windows.
disable-model-invocation: true
---

# /export-windows

Local test builds on the Windows PC. Never upload this output to Steam: releases go out from the MacBook with `/release`, which uploads Windows and macOS together as one build. A Windows-only build set live would take the macOS depot off `default`.

Run from the repo root with the PowerShell tool.

1. `dotnet build "Quartermaster General.csproj" -v q --nologo` exits 0. If it doesn't, stop and report.
2. `.\.claude\skills\export-windows\scripts\export.ps1`. This takes a few minutes and empties `Builds/` first. It fails loudly if the exe, the pck or the release GodotSteam DLL is missing. Report its output verbatim.
3. Tell the user where the build is (`Builds\Quartermaster General.exe`) and its size.
