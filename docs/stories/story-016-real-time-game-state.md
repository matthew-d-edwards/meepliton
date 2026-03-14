---
id: story-016
title: Players receive real-time game state updates without refreshing
status: refined
created: 2026-03-14
---

## What

When any player submits a valid action, the new game state is broadcast in real time to all players in the room — no page refresh required.

## Why

Real-time updates are the core mechanic of the platform — without them, the game isn't playable.

## Acceptance criteria

- [ ] When a player sends an action via `SendAction`, the `GameDispatcher` validates it, applies it, persists the new state, and broadcasts `StateUpdated` to all players in the room's SignalR group
- [ ] All connected clients update their UI immediately when `StateUpdated` is received
- [ ] The state broadcast includes the full current state (not a diff) so any client can render correctly regardless of what they missed
- [ ] Valid actions return no error; the state update is the confirmation
- [ ] The `state_version` on the room record is incremented on every update (for optimistic concurrency)
- [ ] Actions are rejected (and `ActionRejected` broadcast only to the sender) if `IGameModule.Validate` returns a non-null error
- [ ] A `GameFinished` event is broadcast when `GameOverEffect` is returned from the game module

## Notes

- Spec: `docs/requirements.md` §5 (FR-SYNC-01 to FR-SYNC-04), §10 (game module system), §11 (SignalR events)
- Backend agent: `GameDispatcher` + `GameHub` (already scaffolded — verify implementation is complete)
- Frontend agent: `StateUpdated` handler in `RoomPage.tsx`, passes new state down to the game component
- This story validates the core loop; test it with the Skyline game module end-to-end
