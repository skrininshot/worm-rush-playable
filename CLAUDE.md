# Worm Rush — playable ad (Unity 6, 2D)

Portfolio project: a 25-second io-snake playable ad, built stage by stage.
Product, mechanics, architecture and roadmap: `docs/SPEC.md` — read only the sections the current stage needs.
Behaviour reference: `reference/worm-rush-playable.html` (working JS version). When in doubt about how a mechanic should feel, match it.
Handoff between sessions: `docs/PROGRESS.md` — read it first, update it last.

## Workflow (one stage = one session = one PR)
Entry points: `/stage N` starts a stage, `/finish` closes it. Steps they run:
1. Read `docs/PROGRESS.md` and the stage row in `docs/SPEC.md` §5. Do not read the whole codebase; open only files the stage touches.
2. `git checkout main && git pull`, then `git checkout -b stage-N-<short-name>`.
3. Post a short plan (files to add/change, 5–10 lines) and wait for "ok" from the user before writing code.
4. Implement. Commit in small logical steps.
5. Verify: run core tests (`dotnet test CoreTests`) when Core changed; list what the user must check in the Unity editor.
6. Append a stage entry to `docs/PROGRESS.md` (template inside).
7. Push the branch and open a PR to `main` using `.github/pull_request_template.md`. Stop. Do not start the next stage.

## Hard rules
- `Assets/_Project/Core` has **no** `using UnityEngine`. Plain C#, deterministic, unit-tested. Unity code lives in View/Flow/Ad only.
- No Physics/Physics2D. Collisions via own spatial hash.
- No GameObject per worm segment or food item — draw with `Graphics.RenderMeshInstanced`.
- No magic numbers in code: tunables go to `PlayableConfig` (ScriptableObject).
- No external network requests, no third-party packages without asking.
- Do not edit `ProjectSettings/`, `Packages/manifest.json` or scenes by hand-written YAML — tell the user what to change in the editor instead.
- Scope discipline: only the current stage. Ideas for later go to the "Backlog" section of `docs/PROGRESS.md`.

## Environment
- Unity **6000.3.18f1**, URP 2D template, Input System package (use it, not the legacy Input Manager). Target platform: Web.
- Windows machine. The Unity editor runs on the user's machine; you cannot press Play. Anything that needs the editor goes into the PR's "Check in editor" list.
- `CoreTests/` (created in stage 1) is a plain .NET NUnit project that compiles `Assets/_Project/Core/**/*.cs` directly, so core logic is testable without Unity.
- If `dotnet` is not installed, say so and put core tests as Unity EditMode tests in `Assets/_Project/Tests` (asmdef referencing Core) for the user to run in Test Runner.
- Never read `Library/`, `Temp/`, `Logs/`, `*.csproj` — generated, huge, useless.
