---
id: story-017
title: Invalid actions are rejected with a clear message
status: refined
created: 2026-03-14
---

## What

When a player attempts an action that isn't allowed (wrong turn, illegal move), they immediately see a clear error message.

## Why

Without feedback on invalid actions the game feels broken — players don't know why nothing happened.

## Acceptance criteria

- [ ] When `IGameModule.Validate` returns an error string, the `GameDispatcher` sends `ActionRejected` only to the submitting player (not broadcast to all)
- [ ] The frontend displays the rejection message as a toast notification that auto-dismisses after 3–5 seconds
- [ ] The toast uses `<ActionRejectedToast>` from `packages/ui/src/` (platform chrome component)
- [ ] The game state does not change on rejection — the board remains as-is
- [ ] "Not your turn" is the minimum rejection message all game modules must support
- [ ] The toast is visually distinct from success states (uses `--neon-magenta` or error colour, not `--neon-cyan`)

## Notes

- Spec: `docs/requirements.md` §4 (games user stories), §5 (FR-SYNC-04), §11 (SignalR `ActionRejected`)
- Backend agent: `ActionRejected` already wired in `GameDispatcher` — verify and test
- Frontend + UX agents: `<ActionRejectedToast>` component in `packages/ui/src/`
- The `<ActionRejectedToast>` is listed as an existing platform chrome component — confirm it exists or create it
