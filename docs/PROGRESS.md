# Progress log

Short handoff for the next session. Keep each entry under ~15 lines.

## Current state
- Stage **1 — Core simulation**: merged (PR #1).
- Stage **2 — View + controls**: merged (PR #2).
- Stage **3 — Bots + collisions**: PR open, waiting for the user's review/merge. `dotnet test CoreTests` green (67/67); Unity compile NOT run headless (editor had the project open), Core verified with Unity's own Roslyn at C# 9. Next: stage 4, only when the user says `/stage 4`.
- Repo contains: CLAUDE.md, docs/SPEC.md, reference HTML prototype, `Assets/_Project/{Core,Config,View,Flow,Editor,Resources}`, `CoreTests/`.
- Press Play in `SampleScene` (or any scene with a camera): `Flow/Bootstrap` creates everything from code.

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

## Stage 1 — Core simulation (PR #1, merged 2026-10-11)
Done: `Assets/_Project/Core` (WormSim, Worm, Food, SpatialHash, SimConfig, SimEvents, BotBrain, SimRandom, Vec2, Core.asmdef with noEngineReferences). 34 NUnit tests in `CoreTests/` (net8.0, compiles Core sources directly) for growth, collisions, edge death, victim script, determinism, spatial hash — all pass.
Decisions: tunables live in plain-C# `SimConfig` (Core cannot reference the ScriptableObject); `PlayableConfig` will fill it in stage 2/6. `BotBrain` only has the victim script — other bots hold their heading; food seeking, retargeting and edge avoidance are stage 3. Sim clamps dt to 0.033 s like the reference. Food is a struct array (no allocation per spawn).
Known issues / TODO:
- Unity compile check done headless (batchmode, exit 0, `WormRush.Core.dll` built); `.meta` files committed. Nothing to play yet: the first visible result is stage 2.
- The victim-kill test freezes the player (Speed 0). With the player moving straight at 150 vs victim 140 the victim can never catch up, so the stage 3 guarantee must come from the player's steering and the bot AI, not from the script alone.
Where Claude went wrong and how it was caught: the project's `Read(./*.csproj)` deny rule (meant to keep generated Unity projects out of context) also blocked writing `CoreTests.csproj`; Claude stopped and asked instead of routing around it. Tests were written blind, then failed to compile once on NUnit 4 (`CollectionAssert` moved to `NUnit.Framework.Legacy`) — fixed with `Assert.That(..., Is.EqualTo(...))`.

## Stage 2 — View + controls (PR #2, merged 2026-10-11)
Done: the sim is visible and playable. `Game.asmdef` (WormRush.Game) holds `Config/PlayableConfig` (ScriptableObject: wraps `SimConfig` + camera, palette, look), `View/` (`CircleBatch` = `Graphics.RenderMeshInstanced`, `Circle.shader`, `ArenaRenderer`, `FoodRenderer`, `WormRenderer`, `WorldView`, `CameraFollow`, `PointerInput`, `DebugOverlay`) and `Flow/` (`SimRunner`, `Bootstrap`). Look and controls copy the reference: striped worms with shadow and eyes, pulsing glow food, dot grid, pink arena border, zoom-out with length, steer toward the pointer relative to the screen centre. Core got `Steering` and `ViewMath` (+6 tests, 40 total). Editor tools: `ConfigAssetCreator` (makes `Resources/PlayableConfig.asset`), `ScreenshotCapture` (headless render check + CPU benchmark, see CLAUDE.md).
Decisions: no scene editing: `Bootstrap` runs `AfterSceneLoad` and builds everything, uses `Camera.main`. Three instanced layers (arena, food, worms) = 3 batches, +1 per 1023 circles, so a normal frame is 3-4 draw calls. Per-instance colour goes through a `MaterialPropertyBlock` vector array, so colours are converted to linear in `ShaderPalette`. `PlayableConfig` lives in `Resources/` (the only way a code-only bootstrap can load it) and holds the shader reference so builds include it; spec said `Config/` for the asset, the script is there. `SimConfig` is `[Serializable]` and embedded in `PlayableConfig`, so there is one source of truth for tunables. Desktop: after the first click the worm follows the mouse without holding the button (reference does the same); touch keeps the last heading on release. After death a click or R restarts (0.6 s delay) as a stand-in until the stage 4 end card.
Measured (headless, editor Mono): sim step + camera + instance submission 0.14 ms/frame average, 0.44 ms worst, with 4-6 worms and ~400 food; the 540x960, 960x540 and whole-arena renders look right (checked by eye from PNGs). Real FPS and the draw-call count in the Game view still need a human look (DebugOverlay shows both).
Known issues / TODO:
- Bots still only hold their heading (stage 3), so they die on the arena edge within ~10 s and the arena empties; this is expected, not a render bug.
- Input is not exercised headless: `PointerInput`/`SimRunner` were checked by reading and compile only.
- `DebugOverlay` is IMGUI, editor and development builds only; delete or replace with the stage 4 HUD.
- `.claude/settings.json` has an uncommitted change by the user (csproj deny rule removed); left untouched.
Where Claude went wrong and how it was caught: no wrong turn caught this stage. The risk was handing over a renderer nobody had seen, since Play cannot be pressed from here; a headless `camera.Render()` to PNG removes most of it (and exercised the 1023-instance chunking with a zoomed-out shot). What it cannot cover is input and real-time feel.

## Stage 3 — Bots + collisions (PR open, not merged)
Done: `BotBrain` is the reference AI: nearest of 12 random pellets, retarget every 0.8-2 s (per bot, seeded), head for the centre inside 120 units of the edge. Collisions were already in the sim (stage 1); stage 3 made them reachable by real bots and added the climax guarantee. New `SimConfig` knobs: `BotFoodSamples`, `BotRetargetMin/Max`, `BotEdgeMargin`, `VictimSpeedRamp`, `VictimMaxSpeed`, `VictimLoiterDistance`, `VictimKeepOutFraction`, `VictimGuarantee`. 28 new tests, 1 obsolete one replaced (67 total): bot AI, victim rules, and `ClimaxTests` (a harness plays the full default game with stand-in players over fixed seeds).
Acceptance (victim dies on the player 8-15 s into the clip, tap at 3 s; 100 seeds, players that steer round bodies): straight 98%, circling 93%, nearest-food 99%, flees-the-victim 99%; tap at 1 s (window shifts with it, +5..+12 s): 100/95/100/100%. In every run where the player stays alive the victim dies, and only ever on the player's body (the victim never survives 30 s). CI keeps 50 seeds x 4 players at >= 90%.
Decisions (these change the reference behaviour; flip `VictimGuarantee` to get the plain reference):
- The ported AI alone does NOT meet 9/10: the victim wanders up to a whole arena away (2000 across, 140 speed) and the player at 150 can outrun a pure chase. So the guarantee is designed in, all in Core:
  - before the script the victim loiters ~350 from the player (picks pellets at that ring distance, steers outward if closer than 60% of it), so the kill lands 2-6 s after "Block their path!" and not instantly;
  - once scripted its speed ramps +15/s from 140 up to 220, so a straight-running player cannot dodge it for ever;
  - the victim's body never kills the player; the victim is not killed by other bots or the edge (it is pushed back like the player), and before the script it is immune to the player's body as well. After the script only the player's body can kill it;
  - the script overrides edge avoidance (the player's body may be in the outer ring).
- The script window is relative to the tap: kill at tap + 5..12 s (tap at 3 s = the spec's 8-15 s).
- Other bots can and do die on each other, on the player and on the edge; edge avoidance is only a margin, a bot that is busy chasing food can still clip the wall.
Known issues / TODO:
- Unity compile: the editor had the project open and disk is at 5 GB free, so no headless compile. Core was compiled with Unity's bundled `csc.dll`, C# 9, `netstandard 2.1` refs: 0 errors. No View/Flow/Ad files changed this stage. The next headless compile will also re-confirm it.
- Not seen with eyes: how the loitering/victim run looks in the Game view. The sim-level numbers are solid, the feel is not measured.
- The player-death rate in `ClimaxTests` blind runs (no steering round bodies) is 8-15%, mostly the player driving into one bot. That is the stand-in player, but a human who ignores bodies would see the same "SO CLOSE" end.
- Stage 4 hook: the Flow can show "Block their path!" when `sim.Worms[sim.Config.VictimBotIndex].VictimScriptActive` flips to true; the kill is a `Killed` event with `OtherId == player.Id` for that worm.
Where Claude went wrong and how it was caught: the first acceptance probe showed the plain ported AI at 6-40% in the window and the *player* dying in 10-16% of runs. Initial reading was "the bot AI is too weak", but the killer ids were nearly always the same bot: the blind stand-in pilot was driving straight into it. Splitting the numbers by killer id (and giving the pilots a human-like "turn away from bodies" step) separated pilot stupidity from AI behaviour before any AI tuning was done on a wrong signal. The second catch: the guarantee flag silently made the lone test bot (which is also the victim) immortal in 8 older tests; they passed before only because the bot happened to be the victim. `OneBot()` is now a plain bot and `OneVictim()` is explicit.

## Backlog
- Camera smoothing/shake offset hook (stage 5 juice).
- Web build: disable Physics/Physics2D modules (stage 7).
- Bots steer round bodies (the reference does not); would make the arena livelier and the 'SO CLOSE' end rarer (stage 5/6 if wanted).
- Surface the victim knobs (`VictimDelaySeconds`, loiter distance, speed ramp) in the URL-parameter overrides (stage 6).
