# Progress log

Short handoff for the next session. Keep each entry under ~15 lines.

## Current state
- Stage **1 — Core simulation**: PR open, `dotnet test CoreTests` green (34/34). Next: stage 2 after it is merged.
- Repo contains: CLAUDE.md, docs/SPEC.md, reference HTML prototype, `Assets/_Project/Core`, `CoreTests/`.

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
Done: `Assets/_Project/Core` (WormSim, Worm, Food, SpatialHash, SimConfig, SimEvents, BotBrain, SimRandom, Vec2, Core.asmdef with noEngineReferences). 34 NUnit tests in `CoreTests/` (net8.0, compiles Core sources directly) for growth, collisions, edge death, victim script, determinism, spatial hash — all pass.
Decisions: tunables live in plain-C# `SimConfig` (Core cannot reference the ScriptableObject); `PlayableConfig` will fill it in stage 2/6. `BotBrain` only has the victim script — other bots hold their heading; food seeking, retargeting and edge avoidance are stage 3. Sim clamps dt to 0.033 s like the reference. Food is a struct array (no allocation per spawn).
Known issues / TODO:
- Open the project in Unity once so `.meta` files are generated, and commit them.
- The victim-kill test freezes the player (Speed 0). With the player moving straight at 150 vs victim 140 the victim can never catch up, so the stage 3 guarantee must come from the player's steering and the bot AI, not from the script alone.
Where Claude went wrong and how it was caught: the project's `Read(./*.csproj)` deny rule (meant to keep generated Unity projects out of context) also blocked writing `CoreTests.csproj`; Claude stopped and asked instead of routing around it. Tests were written blind, then failed to compile once on NUnit 4 (`CollectionAssert` moved to `NUnit.Framework.Legacy`) — fixed with `Assert.That(..., Is.EqualTo(...))`.

## Backlog
-
