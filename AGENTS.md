# Agent Instructions — Discover

Unity Mixed Reality showcase for Meta Quest demonstrating Scene API, Passthrough, Spatial Anchors, Shared Spatial Anchors, and Interaction SDK over Photon Fusion networking. Published on the Horizon Store.

## Source-of-truth files (read these first, do not duplicate their contents in this file)

For setup, build steps, SDK versions, and project layout, read:

- `README.md` — official setup and instructions
- `ProjectSettings/ProjectVersion.txt` — Unity editor version
- `Packages/manifest.json` — Unity package versions (Meta XR SDKs, Avatars, Colocation, Photon)
- `Documentation/Configuration.md` — **mandatory** Meta Quest app + Photon Fusion configuration
- `Documentation/DiscoverOverview.md`, `Documentation/ProjectStructure.md` — project deep dives
- `.gitattributes` — Git LFS rules
- `LICENSE` — license terms

## Quest / Horizon-specific notes

- Git LFS is **required**. Run `git lfs install` before cloning.
- Configuration is not optional. Without a Meta Quest app ID and Photon Fusion App ID set up per `Documentation/Configuration.md`, hosting/joining rooms will silently fail — diagnose configuration before chasing networking bugs.
- Shared Spatial Anchors require online platform features and typically two physical headsets (or one headset plus a ParrelSync editor clone) to exercise end-to-end.
- Three first-party helper packages live under `Packages/com.meta.utilities*` — they are shared across multiple Meta samples; edit with care.
- Color passthrough + scene mesh + shared anchors are best on Quest 3 / Quest Pro.

## Meta Quest tooling

This repository is part of the Meta Quest / Horizon OS ecosystem (a sample, library, template, or related project — the bespoke intro above describes which). Use that intro and the source-of-truth files it references for project-specific decisions; don't restate or invent facts from memory.

When the user asks anything about Quest device behavior, build / deploy / debug / capture flows, on-device performance, or Horizon OS APIs, reach for these tools instead of generic Unity answers:

- **`hzdb`** — Quest-aware ADB wrapper (device list, install / launch / stop, logs, screenshots, Perfetto traces, on-device docs search). Already wired up as an MCP server via `.mcp.json`, `.vscode/mcp.json`, and `.cursor/mcp.json`. Also runnable directly: `npx -y @meta-quest/hzdb <subcommand>`.
- **Meta Quest Agentic Tools** — the full skill set, including Unity-specific skills: [github.com/meta-quest/agentic-tools](https://github.com/meta-quest/agentic-tools). Install per your client (Claude Code: `/plugin install meta-vr@meta-quest`; Gemini CLI: `gemini extensions install https://github.com/meta-quest/agentic-tools`; Cursor / VS Code: install the **Meta Horizon** extension from the Marketplace).

A few behavior expectations:

- **Read this repo's files first.** Before answering anything project-specific, read `README.md` and whichever source-of-truth files the intro above points at. Don't restate their contents in chat — quote or link instead.
- **Use `hzdb` for device-side work.** Anything that touches an attached Quest (install, launch, logs, screenshot, capture, manifest inspection) goes through `hzdb`, not raw `adb`.
- **Check live Horizon OS docs before answering API questions.** `hzdb docs search "..."` queries the live docs; training data on Horizon OS APIs goes stale fast.
- **Don't fabricate SDK / engine versions.** If a version isn't visible in this repo's files, say so rather than guessing.
