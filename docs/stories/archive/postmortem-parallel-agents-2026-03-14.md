# Post-mortem — parallel agent git conflicts — 2026-03-14

## What failed

Multiple backend and frontend agents were launched in parallel to implement separate stories. All agents operated in the same git working directory (`/home/user/meepliton`) without worktree isolation. Git branch checkouts made by one agent overwrote the working directory HEAD seen by other agents.

Specific failure: the story-006 frontend agent created its auth UI files but found itself on branch `claude/story-009-lobby-Koyne` instead of its own branch `claude/story-006-auth-ui-Koyne`. The agent had been mid-flight when another agent ran `git checkout` in the same directory.

Separately, multiple branches from a prior session were found pushed to origin with real implemented work but no PRs opened and no stories marked done:

- `origin/claude/story-001-registration-GAnxk` — SendGrid email sender + registration endpoint changes
- `origin/claude/story-027-css-tokens-GAnxk` — AppShell component, EF Core migrations, CSS token fixes, App.tsx routing

Those branches represent completed implementation that was never merged or tracked.

## Root cause

Two independent causes:

1. **No worktree isolation.** The Agent tool supports `isolation: "worktree"` which gives each subagent its own git worktree. This parameter was not set when agents were spawned in parallel. All agents shared one working directory; any `git checkout` by one agent immediately affected all others.

2. **No mandatory PR and story-done step in agent workflows.** The backend, frontend, tester, and devops agent definitions all ended their workflow at `git push`. There was no instruction to open a PR with `gh pr create`, and no instruction to update the story file status to `done` and tick acceptance criteria checkboxes. Agents completed implementation and pushed, then stopped — leaving orphaned branches with no PR and stories stuck in `in-progress`.

## Fixes applied

### Agent definitions updated

**`.claude/agents/backend.md`**
- Added step 0: verify branch with `git branch --show-current` before touching any file
- Added step 6: open a PR with `gh pr create` immediately after pushing
- Added step 7: update the story file — set `status: done`, tick acceptance criteria, add PR URL

**`.claude/agents/frontend.md`**
- Added step 0: verify branch before touching any file
- Added step 5: open a PR immediately after pushing
- Added step 6: update the story file to done

**`.claude/agents/tester.md`**
- Added step 0: verify branch before writing any test
- Added step 7: open a PR if tester is the final agent on the branch
- Added step 8: mark the story done if tester is the final agent

**`.claude/agents/devops.md`**
- Added step 0: verify branch before writing any file
- Added PR creation and story-done steps to the commit section

### CLAUDE.md updated

Added a "Running agents in parallel" note under the recommended workflow explaining that parallel agents require `isolation: "worktree"` on the Agent tool call, and that each implementation agent must confirm its branch as step 0.

## How to avoid in future

- When orchestrating parallel story agents, always pass `isolation: "worktree"` to the Agent tool so each agent operates in its own git worktree.
- Every implementation agent now has an explicit step 0 (branch verification) before any file is touched, and explicit final steps (open PR, mark story done) after pushing.
- The `pm` agent should periodically scan for branches pushed to origin that have no PR and no `done` status in their story file, and flag them in `docs/owner/TODO.md`.

## What went well

- The actual implementation work in the orphaned branches (`story-001`, `story-027`) was real and recoverable — the work was not lost, only the PR and tracking step were skipped.
- The error was caught during a trainer post-mortem rather than after a failed merge.
