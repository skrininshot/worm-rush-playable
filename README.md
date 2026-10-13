# Worm Rush — playable ad prototype

A 25-second 2D io-snake playable ad built in Unity 6, end to end, with an agentic AI workflow (Claude Code): architecture, task decomposition and review stay with the developer.

- Spec and roadmap: [docs/SPEC.md](docs/SPEC.md)
- Progress log: [docs/PROGRESS.md](docs/PROGRESS.md)
- Lightweight HTML5 reference (12 KB, single file, MRAID-ready): [reference/worm-rush-playable.html](reference/worm-rush-playable.html)

Genre reference only; no third-party branding or assets are used.

## Web build and hosting

The Unity build is a demo of the architecture and pipeline (an ad network would take the 12 KB HTML reference or a Luna-style conversion). It is a plain static site, so any static host works; Cloudflare Pages is the one in use.

Build (about 3-8 minutes, close the editor first or use the menu **Worm Rush → Build Web (Cloudflare Pages)**):

```bash
"/c/Program Files/Unity/Hub/Editor/6000.3.18f1/Editor/Unity.exe" -batchmode -nographics -quit \
  -projectPath "$(pwd -W)" -logFile build.log \
  -executeMethod WormRush.EditorTools.WebBuild.Run -buildPath Builds/WebGL
```

Output: `Builds/WebGL/` (git-ignored, about 12 MB: the raw `.wasm` is 47 MB, over the 25 MiB per-file limit of Pages, so the build is Brotli-compressed with Unity's decompression fallback; the loader unpacks it in JavaScript and no `Content-Encoding` header is needed). Deploy by dragging that folder into **Workers & Pages → Create → Pages → Upload assets**, or with `npx wrangler pages deploy Builds/WebGL --project-name worm-rush`.

The page template is `Assets/WebGLTemplates/WormRush/index.html`: full screen on a phone, a centred 9:16 column on a desktop.
