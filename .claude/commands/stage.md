---
description: Start roadmap stage N (branch → plan → wait for ok → implement → PR)
argument-hint: <stage number>
---
We are doing stage $ARGUMENTS of the roadmap.

1. Read `docs/PROGRESS.md`, then only the row for stage $ARGUMENTS in `docs/SPEC.md` §5 and the SPEC sections it depends on. Do not scan the codebase; open only files this stage touches.
2. `git checkout main && git pull`, then create branch `stage-$ARGUMENTS-<short-name>`.
3. Reply with a plan of at most 10 lines: files to add/change, how acceptance will be verified, what I must do in the editor. Then STOP and wait for my "ok".
