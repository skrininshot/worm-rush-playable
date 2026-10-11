# Progress log

Short handoff for the next session. Keep each entry under ~15 lines.

## Current state
- Stage **1 — Core simulation**: PR open, **tests not yet run** (see entry below). Next: stage 2 after it is merged.
- Repo contains: CLAUDE.md, docs/SPEC.md, reference HTML prototype, `Assets/_Project/Core`, `CoreTests/` (sources only).

## Stage 0 — done by the user in Unity Hub
- [x] Unity 6000.3.18f1, template Universal 2D, project in the repo root
- [x] Claude scaffold (CLAUDE.md, docs, .claude/ commands and permissions, .gitignore) committed to `main`
- [ ] Platform switched to Web (File → Build Profiles) — do before stage 7
- Folders under `Assets/_Project` are created by Claude in the stages that need them

## Entry template
```
## Stage N — <name> (PR #x, merged YYYY-MM-DD)
Done: …
Decisions: … (why, if non-obvious)
Known issues / TODO: …
Where Claude went wrong and how it was caught: …   ← material for the interview
```

## Stage 1 — Core simulation (PR open, not merged)
Done: `Assets/_Project/Core` (WormSim, Worm, Food, SpatialHash, SimConfig, SimEvents, BotBrain, SimRandom, Vec2, Core.asmdef with noEngineReferences). NUnit test sources in `CoreTests/` for growth, collisions, edge death, victim script, determinism, spatial hash.
Decisions: tunables live in plain-C# `SimConfig` (Core cannot reference the ScriptableObject); `PlayableConfig` will fill it in stage 2/6. `BotBrain` only has the victim script — other bots hold their heading; food seeking, retargeting and edge avoidance are stage 3. Sim clamps dt to 0.033 s like the reference. Food is a struct array (no allocation per spawn).
Known issues / TODO:
- `CoreTests/CoreTests.csproj` is missing: a permission deny rule on `*.csproj` blocked Claude from creating it. Tests were never compiled or run. The csproj content is in the stage 1 session; needs creating by hand or the rule relaxed, then `dotnet test CoreTests` and fix whatever fails.
- Open the project in Unity once so `.meta` files are generated, and commit them.
Where Claude went wrong and how it was caught: wrote ~30 tests without being able to compile them — expect first-run errors.

## Backlog
-
