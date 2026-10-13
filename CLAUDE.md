# Worm Rush — playable ad (Unity 6, 2D)

Portfolio project: a 25-second io-snake playable ad, built stage by stage.
Product, mechanics, architecture and roadmap: `docs/SPEC.md` — read only the sections the current stage needs.
Behaviour reference: `reference/worm-rush-playable.html` (working JS version). When in doubt about how a mechanic should feel, match it.
Handoff between sessions: `docs/PROGRESS.md` — read it first, update it last.

## Autonomy (standing instruction from the user)
Within a stage, work end to end on your own. The user does not want to be pulled into small details.
**But the stage boundary belongs to the user:** when a stage is done, hand over the PR and stop. The user runs the game, plays, and sends corrections on mechanics and graphics. The next stage starts only when the user says so (`/stage N`), normally after merging the previous PR. Never start stage N+1 on your own, never stack PRs on an unreviewed one, never merge PRs.
- **No plan approval.** `/stage N` means: plan briefly in your head, implement, verify, open the PR. Do not stop for an "ok" in the middle.
- **Do not ask about small things** (naming, file layout, default values, which of two reasonable approaches, test details). Pick the sensible default, then record non-obvious choices in the PR "Notes / decisions" and `docs/PROGRESS.md`.
- **Automate everything that can be automated; the user only runs, plays and gives feedback.** Run `dotnet test CoreTests` and the Unity compile check (see Environment) after every meaningful change and before every push; fix errors and failing tests yourself; commit the `.meta` files. Report only the final state.
- **Minimise "Check in editor".** Prefer code that needs no manual editor setup (e.g. bootstrap objects from code instead of hand-editing scenes). List only what needs human eyes or a device: how it feels, how it looks, 60 FPS, touch on a phone. Phrase it as "what to try and what to look at".
- **Unblock yourself.** If a command fails, diagnose and fix it. Never bypass a permission rule; if a rule blocks needed work, say which one in one sentence and continue with everything else.
- **Ask only when the decision is the user's:** changes to the product/roadmap, anything that conflicts with the Hard rules, third-party packages, spending money, accounts.

## Workflow (one stage = one session = one PR)
Entry points: `/stage N` starts a stage, `/finish` closes it. Steps they run:
1. Read `docs/PROGRESS.md` and the stage row in `docs/SPEC.md` §5. Do not read the whole codebase; open only files the stage touches.
2. `git checkout main && git pull`, then `git checkout -b stage-N-<short-name>`. (If the previous PR is not merged yet, tell the user and stop: they review and merge first.)
3. Implement. Commit in small logical steps.
4. Verify: `dotnet test CoreTests` when Core changed; Unity compile check always; commit the `.meta` files Unity generates.
5. Append a stage entry to `docs/PROGRESS.md` (template inside).
6. Push the branch and open a PR using `.github/pull_request_template.md`. Hand it to the user for review and **stop**. Do not start the next stage.

## Hard rules
- `Assets/_Project/Core` has **no** `using UnityEngine`. Plain C#, deterministic, unit-tested. Unity code lives in View/Flow/Ad only.
- No Physics/Physics2D. Collisions via own spatial hash.
- No GameObject per worm segment or food item — draw with instanced mesh draws: `Graphics.DrawMeshInstanced` (`RenderMeshInstanced<T>` throws in the IL2CPP size-optimised Web build).
- No magic numbers in code: tunables go to `PlayableConfig` (ScriptableObject).
- No external network requests, no third-party packages without asking.
- Do not edit `ProjectSettings/`, `Packages/manifest.json` or scenes by hand-written YAML — tell the user what to change in the editor instead.
- Scope discipline: only the current stage. Ideas for later go to the "Backlog" section of `docs/PROGRESS.md`.

## Environment
- Unity **6000.3.18f1**, **built-in render pipeline** and the legacy `UnityEngine.Input` (Active Input Handling = Input Manager). No URP and no Input System package: both drag UIElements/Physics and a lot of managed code into the Web player (7.0 -> 3.0 MB); do not add them back or any other package without asking. Target platform: Web.
- Windows machine, Git Bash. You cannot press Play, but you **can compile the project** headless (below). Only what needs eyes or a device goes into the PR's "Check in editor" list.
- `CoreTests/` is a plain .NET NUnit project (net8.0, hand-written `CoreTests/CoreTests.csproj`) that compiles `Assets/_Project/Core/**/*.cs` directly. Run: `dotnet test CoreTests`. NUnit 4: use `Assert.That(..., Is.EqualTo(...))`, not `CollectionAssert`.
- **Unity compile check** (about 20 s, also generates `.meta` files — commit them):
  ```
  "/c/Program Files/Unity/Hub/Editor/6000.3.18f1/Editor/Unity.exe" -batchmode -nographics -quit \
    -projectPath "$(pwd -W)" -logFile "<scratchpad>/unity-compile.log"
  grep -E "error CS|Scripts have compiler errors|Exiting batchmode" "<scratchpad>/unity-compile.log"
  ```
  Success = exit code 0 and no `error CS` lines. Write the log to the scratchpad and grep it; never read it whole.
- **Render check** (about 30 s, needs the GPU, so run it **without** `-nographics`): draws the real sim through the real renderer and saves a PNG you can `Read` to see the result:
  ```
  Unity.exe -batchmode -quit -projectPath "$(pwd -W)" -logFile "<scratchpad>/unity-shot.log"     -executeMethod WormRush.EditorTools.ScreenshotCapture.Run     -shotPath "<scratchpad>/shot.png" -shotWidth 540 -shotHeight 960 -shotSeconds 6 [-shotViewSpan 480 -shotHeading 0.6 -shotSeed 7]
  ```
  `...ScreenshotCapture.Benchmark` (with `-nographics` is fine) prints CPU ms per frame of sim + draw submission. Use both for any View change; the PR's "Check in editor" is then only feel and device.
  It fails if the editor already has this project open (check `tasklist | grep -i Unity.exe`). Then grep `error CS` in `%LOCALAPPDATA%\Unity\Editor\Editor.log` instead, or ask the user to close the editor only if that is not enough.
- Unity-side code (View/Flow/Ad) is not covered by `dotnet test`; the compile check is its minimum bar. Keep logic in Core where it can be unit-tested, keep View thin.
- Never read `Library/`, `Temp/`, `Logs/` or the generated `*.csproj` in the repo root — huge and useless. (`CoreTests/CoreTests.csproj` is hand-written and fine.)
