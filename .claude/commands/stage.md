---
description: Run roadmap stage N end to end (branch → implement → verify → PR), autonomously
argument-hint: <stage number>
---
We are doing stage $ARGUMENTS of the roadmap. Work autonomously (see "Autonomy" in CLAUDE.md): no plan approval, no questions about small details.

1. Read `docs/PROGRESS.md`, then only the row for stage $ARGUMENTS in `docs/SPEC.md` §5 and the SPEC sections it depends on. Do not scan the codebase; open only files this stage touches.
2. `git checkout main && git pull`, then create branch `stage-$ARGUMENTS-<short-name>`. If the previous stage's PR is not merged, branch from its branch instead and open this PR with that branch as `--base`.
3. Implement. Commit in small logical steps.
4. Verify yourself: `dotnet test CoreTests` when Core changed, and the Unity compile check from CLAUDE.md "Environment" always. Fix everything that fails. Commit generated `.meta` files.
5. Finish as `/finish` does: PROGRESS.md entry, push, PR. Report the PR URL, the final test/compile state, and only the editor checks that cannot be done from here. Do not start the next stage.
