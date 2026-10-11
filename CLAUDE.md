# Worm Rush — playable ad (Unity 6, 2D)

Portfolio project: a 25-second io-snake playable ad, built stage by stage.
Product, mechanics, architecture and roadmap: `docs/SPEC.md` — read only the sections the current stage needs.
Behaviour reference: `reference/worm-rush-playable.html` (working JS version). When in doubt about how a mechanic should feel, match it.
Handoff between sessions: `docs/PROGRESS.md` — read it first, update it last.

## Autonomy (standing instruction from the user)
Work end to end on your own. The user does not want to be pulled into small details.
- **No plan approval.** `/stage N` means: plan briefly in your head, implement, verify, open the PR. Do not stop for an "ok".
- **Do not ask about small things** (naming, file layout, default values, which of two reasonable approaches, test details). Pick the sensible default, then record non-obvious choices in the PR "Notes / decisions" and `docs/PROGRESS.md`.
- **Verify yourself, do not hand verification to the user.** Run `dotnet test CoreTests` and the Unity compile check (see Environment) after every meaningful change and before every push. Fix errors and failing tests yourself; report only the final state.
- **Minimise "Check in editor".** Prefer code that needs no manual editor setup (e.g. bootstrap objects from code instead of hand-editing scenes). List only what truly cannot be checked from here (visual feel, 60 FPS, touch on a real phone).
- **Unblock yourself.** If a command fails, diagnose and fix it. Never bypass a permission rule; if a rule blocks needed work, say which one in one sentence and continue with everything else.
- **Ask only when the decision is the user's:** changes to the product/roadmap, anything that conflicts with the Hard rules, third-party packages, spending money, accounts, or force-pushing/merging into `main`.
- If the previous stage's PR is not merged yet, branch from it (stacked PR, `--base` = that branch) instead of waiting. Do not merge PRs yourself.

## Workflow (one stage = one session = one PR)
Entry points: `/stage N` starts a stage, `/finish` closes it. Steps they run:
1. Read `docs/PROGRESS.md` and the stage row in `docs/SPEC.md` §5. Do not read the whole codebase; open only files the stage touches.
2. `git checkout main && git pull`, then `git checkout -b stage-N-<short-name>` (branch from the previous stage's branch if it is not merged yet).
3. Implement. Commit in small logical steps.
4. Verify: `dotnet test CoreTests` when Core changed; Unity compile check always; commit the `.meta` files Unity generates.
5. Append a stage entry to `docs/PROGRESS.md` (template inside).
6. Push the branch and open a PR using `.github/pull_request_template.md`. Stop. Do not start the next stage unless asked.

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
- Windows machine, Git Bash. You cannot press Play, but you **can compile the project** headless (below). Only what needs eyes or a device goes into the PR's "Check in editor" list.
- `CoreTests/` is a plain .NET NUnit project (net8.0, hand-written `CoreTests/CoreTests.csproj`) that compiles `Assets/_Project/Core/**/*.cs` directly. Run: `dotnet test CoreTests`. NUnit 4: use `Assert.That(..., Is.EqualTo(...))`, not `CollectionAssert`.
- **Unity compile check** (about 20 s, also generates `.meta` files — commit them):
  ```
  "/c/Program Files/Unity/Hub/Editor/6000.3.18f1/Editor/Unity.exe" -batchmode -nographics -quit \
    -projectPath "$(pwd -W)" -logFile "<scratchpad>/unity-compile.log"
  grep -E "error CS|Scripts have compiler errors|Exiting batchmode" "<scratchpad>/unity-compile.log"
  ```
  Success = exit code 0 and no `error CS` lines. Write the log to the scratchpad and grep it; never read it whole.
  It fails if the editor already has this project open (check `tasklist | grep -i Unity.exe`). Then grep `error CS` in `%LOCALAPPDATA%\Unity\Editor\Editor.log` instead, or ask the user to close the editor only if that is not enough.
- Unity-side code (View/Flow/Ad) is not covered by `dotnet test`; the compile check is its minimum bar. Keep logic in Core where it can be unit-tested, keep View thin.
- Never read `Library/`, `Temp/`, `Logs/` or the generated `*.csproj` in the repo root — huge and useless. (`CoreTests/CoreTests.csproj` is hand-written and fine.)
