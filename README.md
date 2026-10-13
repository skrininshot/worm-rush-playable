# Worm Rush — playable ad prototype

A 25-second 2D io-snake playable ad in Unity 6. Drag to steer, eat, block the other worm, land on an end card with a store button.

**Play: https://worm-rush-playable.skrininshot-trk.workers.dev/** (open on a phone, or on a desktop: a 9:16 column in the middle)

Built in **2 days with Claude Code**. Claude wrote and tested the code stage by stage; I reviewed every stage as a human, played it, and sent back corrections on mechanics and UI.

## What is in it

- **Core** (plain C#, no `UnityEngine`, 143 NUnit tests): deterministic worm simulation, own spatial hash instead of Physics2D, bot AI, a guaranteed "kill the victim" climax, flow state machine, UI/juice animation rules.
- **View/Flow/Ad** (Unity): instanced drawing (no GameObject per segment or pellet, 4-5 draw calls), uGUI built from code, synthesized sounds (no audio files), `IAdNetwork` with an MRAID / `window.open` store link.
- Behaviour reference: the 12 KB single-file HTML version in [reference/](reference/worm-rush-playable.html).

## Build size

| | MB (compressed, as served) |
|---|---|
| First Web build | 12.1 |
| Size options, no pipeline asset | 7.0 (wasm 5.2 + data 1.6 + loader 0.2) |
| **Now: no URP, no Input System** | **3.0** (wasm 2.1 + data 0.7 + loader 0.1) |

What got it down: Brotli, IL2CPP "optimize size", wasm "disk size + LTO", no exception support (7.0), then taking the packages out of the project: **URP** (its core forces the Physics and Terrain modules and a lot of managed code) and the **Input System** (it depends on UIElements, which depends on Physics: the whole PhysX backend and the UI Toolkit runtime were in the player for a game that reads one pointer and one key). The project is on the built-in pipeline and the legacy `UnityEngine.Input`; the manifest has only uGUI and the five modules the game uses. Left in the player: Core, Audio, UI (uGUI canvas), IMGUI (uGUI needs it), text rendering, JSON.

The biggest single item now is the IL2CPP metadata (0.9 MB compressed in the data file) and the engine core in the wasm. That is the floor of a Unity Web player; the game itself is a few hundred KB of it.

**Against ad-network limits** (the numbers below are the usual ones, I did not re-check each network's current docs): most networks cap a playable at about 5 MB, Meta at about 2 MB. 3.0 MB fits the 5 MB class as a zip (the `.unityweb` files are already Brotli, so zip adds nothing); as a single self-contained HTML with the files inlined as base64 it is about 4.0 MB, still inside 5 MB, but it needs a converter step that this repo does not have. It does **not** fit a 2 MB cap: that needs the 12 KB HTML reference, not Unity.

## Build and deploy

Close the editor, or use the menu **Worm Rush → Build Web (Cloudflare Pages)**. About 7 minutes (IL2CPP + LTO):

```bash
"/c/Program Files/Unity/Hub/Editor/6000.3.18f1/Editor/Unity.exe" -batchmode -nographics -quit \
  -projectPath "$(pwd -W)" -logFile build.log \
  -executeMethod WormRush.EditorTools.WebBuild.Run -buildPath Builds/WebGL
```

Output: `Builds/WebGL/` (git-ignored). Deploy to Cloudflare (Workers static assets, config in [wrangler.jsonc](wrangler.jsonc)):

```bash
npx wrangler deploy
```

The build script changes player settings only for the duration of the build and puts them back. The page template is `Assets/WebGLTemplates/WormRush/index.html`.

## Docs

- [docs/SPEC.md](docs/SPEC.md): product, mechanics, architecture, roadmap
- [docs/PROGRESS.md](docs/PROGRESS.md): log of every stage, decisions, known issues

Genre reference only; no third-party branding or assets are used.
