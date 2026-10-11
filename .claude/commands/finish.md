---
description: Finish the current stage — verify, log progress, push, open PR
---
Finish the current stage autonomously (see "Autonomy" in CLAUDE.md):

1. Run `dotnet test CoreTests` if Core changed, and the Unity compile check from CLAUDE.md "Environment". Fix failures before continuing; commit generated `.meta` files.
2. Self-review `git diff <base>...HEAD` against the hard rules in CLAUDE.md; fix violations.
3. Append the stage entry to `docs/PROGRESS.md` (template in the file), update "Current state" → next stage. Be honest in "Where Claude went wrong".
4. Commit, `git push -u origin <branch>`, then `gh pr create` with a body following `.github/pull_request_template.md`. Base is `main`.
5. Hand the PR to the user: URL, test/compile result, and a short "what to run, what to try, what to look at" list. Stop — do not start the next stage.
