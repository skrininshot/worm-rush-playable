# Worm Rush — playable ad

A 25-second io-snake playable ad in Unity 6 (2D, URP, Web build).

**[▶ Play it](https://worm-rush-playable.skrininshot-trk.workers.dev/)** — portrait or landscape, mouse or touch.

**Build size:**
<!-- build-size:start -->
not measured yet — run `Tools ▸ Worm Rush ▸ Build Web (5 MB budget)` (see below), it fills this line in
<!-- build-size:end -->

Target: 2–5 MB, the usual ad-network limit. The build step fails above 5 MB.

## What it does

Tutorial hand → eat and grow → "Block their path!" → a rival worm hits your body and bursts into food → end card (AWESOME / SO CLOSE / NICE MOVES) → PLAY NOW opens the store.

- **Deterministic core in plain C#**, no `UnityEngine`: movement, food, collisions, bots, the scripted kill, the whole ad flow. Own spatial hash, no Physics. 143 unit tests (`dotnet test CoreTests`), including a 100-seed check that the scripted kill lands inside the 8–15 s window in 9 of 10 runs.
- **Cheap to render:** worms, food and particles are drawn with GPU instancing, about 4–5 draw calls for the world; no GameObject per segment or pellet.
- **No asset files:** the UI atlas, the tutorial hand and all sounds are generated from code at start-up; the only font is Unity's built-in one.
- **One config:** every tunable lives in the `PlayableConfig` ScriptableObject.
- **Ad-network seam:** `IAdNetwork` (game ready / open store / game end); the Web implementation uses `mraid.open`, falling back to `window.open`.

## How it was made

Built in two days with Claude Code. Claude wrote the code and tests; I set the product and architecture, played every build and sent mechanics and UI back for rework — the juice and UI stage alone went through seven review rounds (button look, tutorial hand, camera distance, edge death, restart button, layout off the screen edges). The log of each stage, including where Claude went wrong and how it was caught, is in [docs/PROGRESS.md](docs/PROGRESS.md).

## Build and run

- Unity **6000.3.18f1**. Open the project, press Play in `SampleScene`.
- Web build under the size budget: `Tools ▸ Worm Rush ▸ Build Web (5 MB budget)`, or headless:
  `Unity -batchmode -quit -projectPath . -logFile web-build.log -executeMethod WormRush.EditorTools.WebBuild.Run`.
  Output goes to `Build/web` (static files, deploy as is). Settings it applies: IL2CPP size optimisation, High managed stripping, engine code stripping, Brotli, no exception support, no data cache.

## Docs

- Spec and roadmap: [docs/SPEC.md](docs/SPEC.md)
- Progress log: [docs/PROGRESS.md](docs/PROGRESS.md)
- Lightweight HTML5 reference of the mechanics (12 KB, single file): [reference/worm-rush-playable.html](reference/worm-rush-playable.html)

Genre reference only; no third-party branding or assets are used.
