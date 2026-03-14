---
name: git-workflow
description: Git and GitHub workflow for Meepliton — branching, commits, PRs, resolving conflicts. Load when helping with git tasks, especially for non-developer contributors.
user-invocable: false
---

See full guide in `.claude/skills/GIT-WORKFLOW.md`. Key conventions below.

## Branch names

| Purpose | Pattern |
|---|---|
| New game | `add-{gameid}-game` |
| Bug fix | `fix-{description}` |
| Improvement | `update-{description}` |
| Docs only | `docs-{description}` |
| Claude work | `claude/{description}-{sessionId}` |

**Never push directly to `main`.** Always branch → PR → merge.

## Daily loop

```bash
git checkout main && git pull          # start fresh
git checkout -b your-branch-name      # new branch
# ... make changes ...
git add {specific files}              # stage (not git add -A)
git commit -m "type: description"     # commit
git push -u origin your-branch-name  # push
# open PR on GitHub
```

## Commit message types

`feat` · `fix` · `refactor` · `test` · `docs` · `chore` · `design`

## PR workflow

```bash
gh pr create --title "Add Sushi Go game" --base main
gh pr checks --watch          # watch CI
gh pr merge --squash          # merge when green
```

## Recovery

```bash
# Undo last commit (keep changes)
git reset --soft HEAD~1

# Discard unstaged changes to a file
git checkout -- {file}

# See what changed
git diff
git log --oneline -10
```
