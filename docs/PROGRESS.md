# Progress log

Short handoff for the next session. Keep each entry under ~15 lines.

## Current state
- Stage **1 — Core simulation**: merged (PR #1).
- Stage **2 — View + controls**: merged (PR #2).
- Stage **3 — Bots + collisions**: merged (PR #3).
- Stage **4 — Flow + end card**: merged (PR #4).
- Stage **5 — Juice**: merged (PR #5). Review round 1, the "ultra-dopamine" buttons and end card, is a follow-up PR (`stage-5-ui-juice`), waiting for the user's review/merge. `dotnet test CoreTests` green (143/143), Unity compile clean (headless, exit 0), render check of the kill moment, the tutorial finger and the end card. Review rounds 2-6 (PLAY NOW colours, finger hint, label rim, closer camera, end-card autopilot + edge death, restart button) are on the same branch. Next: stage 6, only when the user says `/stage 6`.
- Repo contains: CLAUDE.md, docs/SPEC.md, reference HTML prototype, `Assets/_Project/{Core,Config,View,Flow,Editor,Resources}`, `CoreTests/`.
- Press Play in `SampleScene` (or any scene with a camera): `Flow/Bootstrap` creates everything from code.

## Stage 0 — done by the user in Unity Hub
- [x] Unity 6000.3.18f1, template Universal 2D, project in the repo root
- [x] Claude scaffold (CLAUDE.md, docs, .claude/ commands and permissions, .gitignore) committed to `main`
- [x] Platform switched to Web (done by the first Web build, see Stage 5 review round 8)
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

## Stage 3 — Bots + collisions (PR #3, merged 2026-10-12)
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

## Stage 4 — Flow + end card (PR #4, merged 2026-10-12)
Done: the whole 25-second loop is playable in the editor. Core `PlayableFlow` (Intro → Tutorial → Play → Climax → EndCard, 11 tests; +3 for `ViewMath.UiScale`, 81 total) decides what the screen shows and when it ends: kill + 1.8 s, 25 s after the first tap, or player death + 1.1 s (death overrides a pending kill and gives SO CLOSE; AWESOME needs a kill, NICE MOVES is a 25 s survivor). `Ad/`: `IAdNetwork` (GameReady, OpenStore, GameEnd), `EditorAdNetwork` (logs, opens nothing), `WebAdNetwork` + `Plugins/WebGL/WormRushAd.jslib` (`mraid.open`, else `window.open`, retried on the next pointerup if the popup blocker eats it), `AdNetworks` factory. `View/PlayableUi` + `UiKit`: uGUI canvas built from code with the length counter, persistent CTA, tutorial hand, "Block their path!" and the end card (title pop, pulsing button), layout in the reference's CSS px (390x844). `SimRunner` became `PlayableRunner`: input, sim, flow, draw; a tap on the CTA or anywhere on the end card calls `IAdNetwork.OpenStore`. `ScreenshotCapture` got `-shotState play|tutorial|end` and draws the UI too.
Decisions:
- The state machine lives in Core (`Core/PlayableFlow.cs`), not in `Flow/` as SPEC §3 sketches, so `dotnet test` covers it; the Unity side of Flow only drives it. Timings are `SimConfig` fields (`IntroSeconds`, `BlockHint*`, `EndAfter*`, `MaxPlaySeconds`); texts, store URLs and button colours are `PlayableConfig` fields.
- UI is uGUI `Text` + two procedural sprites (circle, 9-sliced pill), built-in `LegacyRuntime.ttf`. No TextMeshPro (needs editor-imported resources), no IMGUI (GC, slow on phones), no EventSystem, no CanvasScaler: the CTA is hit-tested by `PlayableUi.IsOverCta` and the scale is `ViewMath.UiScale` (tested), set from the render-target size so the render check lays out exactly as a device does. Canvas is Screen Space - Camera with sorting order 100, so it is captured by `camera.Render()`.
- Reference semantics kept on purpose: an un-tapped ad never ends; the sim keeps running under the end card; pressing the CTA never starts the run; a pending end is not replaced by the 25 s limit.
- The "hand" is the reference's white dot sliding on a track with "DRAG TO MOVE" above it, not a drawn hand.
- Stage 2's click-after-death restart is gone; `R` restarts in editor and development builds only.
- Store URLs are the reference's placeholders (`apps.apple.com`, `play.google.com/store/apps`); real ones go into `PlayableConfig` / the stage 6 URL parameters.
Known issues / TODO:
- `WebAdNetwork` and the `.jslib` have never run: the platform is still not Web and there is no Web build before stage 7. Compiled with `UNITY_WEBGL` defined against Unity's DLLs, nothing more.
- Draw calls: not measurable headless. By batching rules ~3-4 world + ~6 UI (fonts and the two sprites break batches); `DebugOverlay` prints the real `batches` in the editor.
- The built-in dynamic font ships in the Web build; stage 7 may swap it for a subset atlas.
- Not seen with eyes: pop, pulse and the dot animation (only stills), touch hit area of the CTA on a real phone.
- `PlayableConfig.RestartDelaySeconds` was removed; the stale key stays in `PlayableConfig.asset` until it is re-saved.
Where Claude went wrong and how it was caught: (1) the first flow tests passed the victim through the scenario with a free-running AI, so "far away" was only true for a few seconds and the kill came early; caught by reasoning about the loiter ring before running them, and fixed by parking the victim every step. (2) One test encoded a rule the reference does not have (the 25 s limit overriding a pending kill end); it failed, and the right answer was to delete the test, not to change the flow. (3) A screenshot crop looked like a pellet was drawn over the "Length" text; zooming to pixels showed the text was on top all along. The canvas sorting order stayed in as a cheap guard against the 2D renderer's distance sort. (4) The headless compile was blocked by the open editor; the first check was csc against Unity's DLLs and the built-in ugui sources (clean), then the user closed the editor for the real compile and render check.

## Stage 5 — Juice (PR #5, merged 2026-10-13)
Done: shake, burst particles, popups and sounds, all driven by the sim's events. Core: `JuiceConfig` (every tunable + the sound cues), `JuiceState` (the rules: which event gives which shake / burst / popup / sound), `ScreenShake`, `ParticlePool` and `PopupPool` (fixed arrays, no allocation), `ComboMeter`, `ToneSynth` (17 tests, 98 total). View: `ParticleRenderer` (4th instanced layer above the worms, so a normal frame is 4-5 draw calls), `SoundBank`, popups inside `PlayableUi`, `CameraFollow.Apply(player, shakeX, shakeY)`. `PlayableRunner` runs `JuiceState` after the sim/flow step and plays what it asks for. `ScreenshotCapture` got `-shotState kill [-shotAfter s]` and draws the juice.
Reference values kept: shake 0.35 s on a kill / 0.5 s on death (18 px per second left), 26 particles at 60-260 speed living 0.4-0.9 s, popups "+1 KILL!" at 1.6x and "+N" at 0.8x rising 40/s for 1.2 s, eat blip 420 Hz + 28 Hz per combo step (24 max), kill = square 660 then 990, death = sawtooth 140, end card = triangle 523 then 784.
Decisions:
- The rules live in Core (`JuiceState`), not in `View/Juice.cs` as SPEC §3 sketches, so `dotnet test` covers them; the View only draws and plays. Same call as `PlayableFlow` in stage 4.
- Sounds are synthesized at start-up from the cues in `JuiceConfig` (`AudioClip.Create`, one clip per combo step for the eat blip), so there are no audio files and the build stays small. Delayed second notes are timed in `SoundBank.Update`, not `PlayDelayed`, to stay on the simplest Web audio path.
- Deviations from the reference, all one value away from it: the eat streak starts over after `ComboWindowSeconds` (1.5 s; 0 = the reference's never-resetting streak); nothing but burst particles reacts under the end card (the reference keeps blipping on pellets over the AWESOME sound); the persistent CTA breathes by `CtaPulseAmount` (5%; 0 = the reference's still button). The end-card button pulse was already stage 4.
- Popups are uGUI `Text`, a pool of `PopupCapacity` rasterised at one size and scaled to the world, so the zooming camera never regenerates the font atlas. Shake amplitude is in UI units (390x844 reference px) and converted to world units by the screen height.
- Particle drag is `exp(-5 dt)` (the reference's x0.92 per 60 Hz frame), so it no longer depends on frame rate.
Known issues / TODO:
- Not heard and not felt: the render check shows a still of the kill (burst, popup, shake 0.07 s left), but the shake, the pitch ramp and the volumes have never been played. The values are the reference's, which is the best guess until someone listens.
- `SoundBank` has never run in a Web build (Unity WebGL audio starts after the first user gesture, which the first tap is). Same status as `WebAdNetwork`: stage 7.
- No mute: Web audio is on from the first tap. Networks usually want a mute hook; stage 6/7 if wanted.
- `PlayableConfig.asset` was not re-saved: the new `Juice` block takes its C# defaults until the asset is next saved in the editor.
- Particles are not in `ScreenshotCapture.Benchmark` (it draws without juice); a burst is at most 26 circles per death, so the cost is small, but it is not measured.
Where Claude went wrong and how it was caught: first draft of `JuiceConfig` carried a `[Tooltip]`, a UnityEngine attribute in Core, and a leftover placeholder field; caught by re-reading the file before the first test run, not by the compiler (the CoreTests project would have refused the attribute, Unity's asmdef has `noEngineReferences`, so it would have been caught there too). The reference's `ended` flag is set when the card appears, not when it is scheduled, so `JuiceState` reads the end-card state of the previous frame: a kill on the very frame the card appears still gets its shake and popup, as in the reference.

### Stage 5, review round 1 — "ultra-dopamine" buttons (follow-up PR, not merged)
Feedback: AWESOME card and PLAY NOW buttons felt flat; wanted a UI that lives, animates, explodes on every action.
Done: all of it is `UiJuice` in Core (springs, timeline, pools, 28 new tests, 126 total) drawn by `PlayableUi`. Both buttons: squash-and-stretch spring on every press, double heartbeat pulse, wobble, breathing halo, a beam of light sweeping the face (stencil-masked to the pill), hop + sparks every few seconds, mouse hover grows the end button (desktop only). A press anywhere on the end card or on the CTA throws confetti, stars and a shockwave ring out of the finger and plays a three-note tap. End card: white flash, rotating sunburst, title as one Text per letter (staggered drop-in with tilt and overshoot, then a wave with a gold highlight running along it), confetti blast out of the title, subtitle rises in, the button springs in with sparkles and a pop sound, then throws sparks. HUD: the CTA and the length counter bump on every pellet and harder on a kill, combo stars every 5th pellet, "Block their path!" pops in and pulses. `UiKit` now paints ONE atlas (circle, pill, square, star, ring, glow, beam, rays), so all UI images batch together; confetti lives on a nested canvas so it re-batches alone.
Decisions: `PlayableConfig.CtaPulseAmount/Seconds` are gone (superseded by `UiJuice.Heartbeat*`; the stale keys stay in the asset until re-saved). New `PlayableConfig.UiJuice` block holds every number, defaults apply until the asset is re-saved. `ScreenshotCapture` steps the UI in real frames (`-shotUiSeconds`, `-shotPress 1`), so springs and confetti are where they would be on a device.
Known issues / TODO:
- Not felt or heard: the spring stiffness, the sweep timing and the three tap/pop blips are guesses; all are `UiJuiceConfig` numbers. If it is too much on a phone, lower `TapConfetti`, `EndSparklesPerSecond` and `FxCapacity` first.
- Editor was open, so the headless compile ran with Unity's bundled csc (0 errors) and the render check on a copy of the project.

### Stage 5, review round 2 — PLAY NOW colours, tutorial finger (same PR)
Feedback: yellow button with black text felt dull; the "DRAG TO MOVE" dot on a slider was not obvious.
Done: both PLAY NOW buttons are a lime gradient with a white label, a dark-green outline and an extruded drop, plus a lighter top half (gloss, clipped to the pill by the existing mask); the halo is green too. The slider is gone: a pointing hand lands, presses (ripple under the fingertip, soft light), drags once round a circle with a fading dot trail, lifts with a little pop and repeats; "DRAG TO MOVE" stays above the circle. The loop is `TutorialGesture` in Core (a pure function of time, 9 tests, 135 total, numbers in `UiJuiceConfig.Tutorial*`); the hand is painted into the UI atlas from signed distances (layers back to front, each with its own outline), so there is still no image asset.
Decisions: colours are `PlayableConfig` fields (new `CtaTextOutlineColor`, `EndButtonTextOutlineColor`); the persisted asset never stored the button colours, so the C# defaults apply with no re-save. The end-card button uses the same lime as the CTA so "PLAY NOW" is one identity; flip `EndButton*` fields to separate them. Tutorial time is the UI clock since the hint appeared, not sim time.
Known issues / TODO: finger speed and loop length are guesses (`TutorialDragSeconds`, `TutorialRestSeconds`); the hand looks like a cartoon cursor, not a photo of a hand, on purpose. Checked in the render check at 540x960 on a project copy (editor was open): compile clean, stills of three moments of the loop, play and end card.

### Stage 5, review round 3 — PLAY NOW label rim (same PR)
Feedback: the shadow under the PLAY NOW text was far too wide and its edge was ragged.
Done: Unity's `Shadow` + `Outline` pair is gone from both PLAY NOW labels. Outline copies the glyphs on four diagonals and the shadow was copied again by it, so the drop turned into a wide comb. New `TextRim` (in `UiKit.cs`, via `UiKit.AddRim`) draws the glyphs on a ring of 12 points, and once more on the same ring moved down by the drop: an even edge with a short extruded lip. Smaller numbers: CTA rim 1.5 + drop 1 (was 2 + 1.5), end button rim 2 + drop 1.5 (was 3 + 2).
Decisions: the other labels (hint, tutorial, popups, end-card title letters) still use `Outline`/`Shadow`; the title letters' stacked shadows look a little rough too, left alone until asked.
Known issues / TODO: checked in the render check at 1080x1920 (play and end card), compile clean.

### Stage 5, review round 4 — closer camera (same PR)
Feedback: the worm looks too small against the arena; also the "CTA pulse" check item from PR #5 was unclear (it meant the PLAY NOW button's breathing, not anything on the worm; the item was stale once `CtaPulseAmount` was replaced by `UiJuice.Heartbeat*`).
Done: `PlayableConfig.ViewSpan` 480 -> 340 (default in code and in `Resources/PlayableConfig.asset`, which stores the value): the shorter screen side now shows 340 world units at length 0, so everything is about 1.4x bigger. The reference used 480; this is a deliberate deviation. Checked on 540x960 renders.
Known issues / TODO: `ZoomOutPerLength` (0.002) is untouched, so a long worm still zooms the view out; lower it if the worm should stay big as it grows.

### Stage 5, review round 5 — autopilot under the end card, edge kills the player (same PR)
Feedback: under the PLAY NOW screen (input blocked) the worm just flew on in its last direction; it should behave like an ordinary bot. And the player should die on touching the arena edge.
Done: `WormSim.Autopilot`: while true the player's worm is steered by `BotBrain.Think` (nearest of 12 pellets, retarget every 0.8-2 s, centre when inside the edge margin) and `SetPlayerHeading` is ignored. `PlayableRunner` switches it on whenever the flow is in `EndCard`. `SimConfig.PlayerEdgeDeath` (default true): after the first tap the player's head touching the edge kills it (`Killed` + `PlayerDied`, killer -1, remains drop as usual) and the flow ends with SO CLOSE after the usual 1.1 s. 8 new tests (143 total).
Decisions:
- Deliberate deviation from the reference, which only pushes the player back. `PlayerEdgeDeath = false` gives the reference. Before the first tap the player is still pushed back (it only circles at the centre anyway).
- The scripted victim keeps its shield: it is pushed back from the edge, never killed by it, so the climax guarantee stays intact.
- The autopilot is a plain bot, so under the end card it can still die (edge clip, another worm's body); then the camera stays on the remains, the arena goes on.
- `ClimaxTests` stand-ins now avoid the edge like a human would (`ClimaxHarness.Human` = edge-aware + body-aware); without that every "straight" run died on the wall at ~7 s. Acceptance numbers are unchanged (all four pilots stay >= 90%).
Known issues / TODO: not seen with eyes (no render check: the change is sim-side, the end-card stills are unaffected). A player who steers toward the edge now ends the ad with SO CLOSE; if that is too harsh, ideas for later: a warning vignette near the border, or a short grace period.

### Stage 5, review round 6 — restart button (same PR)
Feedback: a small restart button, so the playable can be hosted (Cloudflare) and replayed.
Done: a round icon button in the top-right corner (34 UI units disc, 44 hit area, 10 from the edges), always visible, drawn above the end card but under the confetti. A press starts a new run (`PlayableRunner.NewRun`, the same path as the editor's `R`), spins the arrow once and dips the disc; the press is the button's alone: it does not steer, start the run or open the store, and on the end card it wins over "tap anywhere". The icon (a 270 degree ring with an arrowhead) is one more cell in the generated UI atlas (`UiKit.Restart`), so there is still no image asset. `PlayableConfig.ShowRestartButton` (default true) and `RestartBackColor`/`RestartIconColor`.
Decisions: it is a config switch, not a build flag, so a hosted demo and an ad-network build differ only by one value (stage 6 can expose it as a URL parameter). `IAdNetwork.GameEnd` fires again at the end of every run, as `NewRun` resets `_endReported`: right for a demo, wrong for a real network, which is the reason to switch the button off there.
Known issues / TODO: not tapped on a device (touch hit size 44 units is the usual minimum); checked in the render check at 540x960 and 1080x1920 (play and end card), compile clean, no Core change (143 tests green).

### Stage 5, review round 7 — UI off the edges, bigger buttons (same PR)
Feedback: edge elements too close to the screen edge; PLAY NOW on the play screen ~1.5x bigger and higher; the score under the phone's camera; SO CLOSE / AWESOME and the end PLAY NOW 1.25-1.5x bigger without touching the edge.
Done (all in `PlayableUi`, UI units of the 390x844 layout): length counter centre 26 -> 74 from the top, "Block their path!" 62 -> 112, restart button 10 -> 22 from the side and 52 from the top (level with the counter). Persistent CTA 1.5x (font 17 -> 26, height 46 -> 69, pad 30 -> 45) and 18 -> 54 off the bottom. End card: title font 44 -> 62, button font 24 -> 34 / height 62 -> 86 / pad 46 -> 62, subtitle 18 -> 20 in a 330 box; the stack was re-spaced (title y 98, subtitle 30, button -62).
Decisions: the title fits the screen: it shrinks until it clears `EndSideMargin` (32) on each side; if that would be below 55 (1.25x the old 44) and it has a space, it breaks into two lines instead (NICE MOVES! -> NICE / MOVES!, the lower line where the one-line title sat). Fixed offsets, not `Screen.safeArea`: the render check and the Web build have no reliable safe area, and ~50 units is clear of every cut-out in use.
Known issues / TODO: not seen on a device; if the camera still clips the counter on some phone, raise `LengthTop` and `RestartMarginTop`. Checked in the render check at 468x1013 (play, SO CLOSE, NICE MOVES! with a temporary title), compile clean, no Core change.

### Stage 5, review round 8 — Web build for Cloudflare Pages (same PR)
Request: build the playable and put it on Cloudflare Pages. This was the first Web build of the project.
Done: `Editor/WebBuild.cs` (menu **Worm Rush > Build Web**, or `-executeMethod WormRush.EditorTools.WebBuild.Run -buildPath Builds/WebGL`) makes a release build into `Builds/WebGL` (git-ignored), prints every file's size and flags anything over the 25 MiB Pages limit. Page template `Assets/WebGLTemplates/WormRush/index.html`: full screen on a phone, a centred 9:16 column on a desktop, DPR capped at 2, own progress bar. README has the build and deploy commands.
Found by the first Web build (not visible in the editor): (1) the raw `.wasm` is 46.7 MB, over the 25 MiB per-file limit of Pages. Build uses Brotli + decompression fallback (the loader unpacks `.unityweb` in JS, no `Content-Encoding` needed): wasm 8.2 MB, 12.1 MB in total. (2) The world did not draw at all, only the UI: the build stripped the `INSTANCING_ON` variant of `WormRush/Circle` because only a run-time material used it. `Resources/CircleInstanced.mat` (instancing on, made by the build script) keeps it; the log now shows 2 of 2 variants.
Decisions: the build script sets compression/template through `PlayerSettings` and puts the old values back, so `ProjectSettings` is unchanged by it. Unity re-serialises URP assets and two ProjectSettings files on every build; those diffs are not committed.
Known issues / TODO: checked by loading the build in a browser from a static server that sends no `Content-Encoding` (like Pages): loads, draws, UI and a kill popup show. Not checked: touch on a phone, sound in the Web build, `WebAdNetwork`/`.jslib` (store link on a real tap), real FPS. The Unity "Made with Unity" splash shows before the game (Personal licence). The wasm is big for what it does (stage 7: strip Physics/Physics2D and other modules). The active editor platform is now Web (the build switched it).

## Backlog
- Stage 6: `ShowRestartButton` as a URL parameter (off by default for real ad networks); decide whether a restart should reset or re-fire `GameEnd`.
- Flow: if the 25 s limit should cut a pending kill/death end short, make `PlayableFlow` take the earlier of the two.
- Camera smoothing (the rigid follow is the reference's; shake itself is done).
- Web build: disable Physics/Physics2D modules (stage 7).
- Bots steer round bodies (the reference does not); would make the arena livelier and the 'SO CLOSE' end rarer (stage 6 if wanted).
- Surface the victim knobs (`VictimDelaySeconds`, loiter distance, speed ramp) in the URL-parameter overrides (stage 6).
- Mute hook / volume URL parameter (`JuiceConfig.MasterVolume` is already there), and the Web audio check (stage 6/7).
