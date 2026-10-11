---
description: Finish the current stage — verify, log progress, push, open PR
---
Finish the current stage:

1. Run `dotnet test CoreTests` if Core changed (skip if CoreTests does not exist yet). Fix failures before continuing.
2. Self-review `git diff main...HEAD` against the hard rules in CLAUDE.md; fix violations.
3. Append the stage entry to `docs/PROGRESS.md` (template in the file), update "Current state" → next stage. Be honest in "Where Claude went wrong".
4. Commit, `git push -u origin <branch>`, then `gh pr create --base main` with a body following `.github/pull_request_template.md`.
5. Print the PR URL and the "Check in editor" list. Stop — do not start the next stage.
