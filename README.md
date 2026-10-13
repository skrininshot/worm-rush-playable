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
| **Now** | **7.0** (wasm 5.2 + data 1.6 + loader 0.2) |

What got it down: Brotli, IL2CPP "optimize size", wasm "disk size + LTO", no exception support, no render pipeline asset in the Web build (the editor stays on URP), a one-camera build scene, and URP post-processing resources left out (the film grain textures alone were 2.5 MB raw).

This is above the 2-5 MB that ad networks usually accept, and I am not going to pretend otherwise: Unity's player is the floor, not the game. On a throw-away copy of the project without URP (built-in pipeline) the same game measured 5.5 MB with full exception support, so the floor is roughly 5 MB; that change is separate (it removes URP from the project). In production this would go through a playable converter, or ship as the 12 KB HTML reference.

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
