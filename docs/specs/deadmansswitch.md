# Spec: Dead Man's Switch

**Status:** Agreed
**Date:** 2026-03-23
**Story:** story-030
**Game ID:** `deadmansswitch`
**Authors:** analyst + architect

---

## Problem

Meepliton's two live game modules (Skyline, Liar's Dice) cover tile placement and dice-based hidden bidding. The group needs a fast party game that plays in 20–30 minutes, supports 3–6 players, and rewards social reading rather than calculation. Beyond the entertainment gap, Dead Man's Switch exercises two platform capabilities that neither existing game has used together: per-player hidden disc ordering (stricter hidden state than Liar's Dice dice) and a multi-phase turn structure with a hand-off mid-round, where one player triggers bidding and a different player becomes the Challenger.

---

## Solution

A self-contained C# game module (`Meepliton.Games.DeadMansSwitch`) implements `IGameModule` and `IGameHandler` directly (not `ReducerGameModule`) to support the `GameOverEffect` required when the game ends. The matching React module (`apps/frontend/src/games/deadmansswitch/`) consumes projected state via the standard `GameContext`. Both are scaffolded with `scripts/new-game.ps1` and the frontend entry point is registered in `registry.ts`.

State projection (`HasStateProjection = true`) hides opponent disc types during `Placing`, `Bidding`, and `Revealing` phases. The frontend renders face-down discs using `StackCount` — no `Hidden` sentinel value is used; opponent `Stack` arrays are projected as empty (`[]`) and `StackCount` carries the count. During `Revealing`, each flipped disc becomes visible as it is flipped, communicated via `LastFlip` and the disc's `Flipped` flag.

The game stores no supplementary tables in v1. State lives entirely in the platform's `rooms.game_state` JSONB column. `DeadMansSwitchDbContext` is scaffolded with an empty initial migration using a game-scoped migration history table.

---

## Theme

Heist / bomb-defusal. Language throughout:

| Game concept | Theme language |
|---|---|
| Disc | Device |
| Skull disc | Trigger |
| Rose disc | Dud |
| Placing a disc | Arming a device |
| Opening a bid | Committing to the job |
| The bid number | Target count |
| Winning a reveal | Defusing the switch |
| Player mat | Mission board |

The in-game room view carries this theme via `data-game-theme="deadmansswitch"` on the game root element, scoping CSS token overrides. The lobby, header, and waiting screen remain in the platform default theme (per ADR-008 and the Liar's Dice precedent).

Aesthetic direction: black-ops / industrial — very dark near-black backgrounds, muted steel surfaces, sharp amber-yellow accents for active and interactive elements, tight uppercase sans-serif labels, monospaced numerals for counts and bids. Avoid military-cliché greens, cartoon explosions, and neon.

Suggested CSS token overrides (for UX review):

```css
[data-game-theme="deadmansswitch"] {
  --color-background:     #0a0a0c;
  --color-surface:        #121218;
  --color-surface-raised: #1c1c26;
  --color-surface-hover:  #22222e;
  --color-primary:        #d4a017;
  --color-on-primary:     #0a0a0c;
  --color-border:         #2a2a38;
  --color-text:           #d8d8e0;
  --color-text-muted:     #6a6a80;
  --radius-sm:            2px;
  --radius-md:            3px;
}
```

---

## Rules

3–6 players. Each player starts with 4 disc tokens: 3 roses + 1 skull.

**Phase 1 — Placing**
In seat order, each player must either place one disc face-down on top of their stack (`PlaceDisc`) or, if they have already placed at least one disc this round, open bidding by committing to a target count (`StartBid`). On the first pass around the table every player must place before anyone may open bidding. After that first pass any player whose turn comes up may open bidding instead of placing.

**Phase 2 — Bidding**
The player who called `StartBid` sets the opening number and is first in the bidding rotation. Going around in seat order, each subsequent player must raise the bid (`RaiseBid`) or pass (`Pass`). A passed player is skipped for the rest of the bidding phase. The last non-passed player becomes the Challenger automatically (no action required) and the phase transitions to `Revealing`. If any player raises the bid to the total number of face-down discs on the table, they immediately become the Challenger.

**Phase 3 — Revealing**
The Challenger flips discs one at a time via `FlipDisc`. They must flip all of their own discs top-to-bottom (most-recently-placed first) before touching any opponent stack. After their own stack is fully flipped they freely choose which player's stack to draw from (`FlipDisc` with `targetPlayerId`).

- If the flipped count reaches the bid number without hitting a skull: **success** — Challenger earns 1 point.
- If a skull is flipped: **failure** — reveal stops immediately.

**Success:** First player to reach 2 points wins. On win, `GameOverEffect` is emitted and phase transitions to `Finished`. On non-winning success, phase transitions to `RoundOver`; the same player who last started bidding leads the next round (seat 0 in round 1).

**Failure — own skull:** Phase transitions to `DiscardChoice`. The Challenger must send `DiscardDisc` naming one disc type (`Rose` or `Skull`) to permanently remove from the game. If the Challenger owns only one disc type, the server auto-discards without waiting for a `DiscardDisc` action and moves directly to `RoundOver`. The Challenger leads the next round.

**Failure — opponent skull:** The server immediately picks one of the Challenger's discs at random to permanently remove. No player action is required; the phase transitions directly from `Revealing` to `RoundOver`. The skull owner leads the next round.

**Elimination:** A player whose total remaining discs (roses + skull) reach 0 is eliminated (`Active = false`). They spectate with full state visibility. If only one player remains active the phase transitions to `Finished`, that player is `Winner`, and `GameOverEffect` is emitted.

**Round reset:** On `StartNextRound`, all active players reclaim their non-discarded discs (stacks cleared, hands restored to remaining counts). Phase returns to `Placing` from the designated first player.

---

## State shape

### C# records

```csharp
// All enums require [JsonConverter(typeof(JsonStringEnumConverter))]

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DeadMansSwitchPhase
{
    Placing,
    Bidding,
    Revealing,
    DiscardChoice,   // Challenger hit own skull; awaiting Challenger's DiscardDisc
    RoundOver,
    Finished
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DiscType { Rose, Skull }

public record DeadMansSwitchState(
    DeadMansSwitchPhase Phase,
    List<DevicePlayer>  Players,
    int                 CurrentPlayerIndex,        // whose turn it is in all active phases
    int                 CurrentBid,                // 0 when not in Bidding/Revealing
    int                 TotalDiscsOnTable,          // cached sum of all StackCounts
    string?             ChallengerId,              // set when phase enters Bidding
    int                 NextRoundFirstPlayerIndex, // index into Players
    FlipLog?            LastFlip,                  // last disc flipped (for animation)
    string?             Winner,                    // userId, set in Finished
    int                 RoundNumber
);

public record DevicePlayer(
    string         Id,
    string         DisplayName,
    string?        AvatarUrl,
    int            SeatIndex,
    List<DiscSlot> Stack,       // ordered top-to-bottom; projected to [] for opponents
    int            StackCount,  // always == Stack.Count; always visible to all
    int            RosesOwned,  // permanent rose count (decrements on discard)
    bool           SkullOwned,  // permanent skull ownership (false if skull discarded)
    int            PointsWon,   // 0, 1, or 2
    bool           Active,      // false = eliminated
    bool           Passed       // true during Bidding if this player passed
);

public record DiscSlot(
    DiscType Type,    // visible only to owner and after Challenger flips (Flipped = true)
    bool     Flipped  // true after Challenger reveals this disc
);

public record FlipLog(
    string   FlippedByPlayerId,
    string   StackOwnerId,
    DiscType Result,
    int      FlipNumber    // 1-indexed count toward bid
);
```

### TypeScript mirror

```typescript
export type DeadMansSwitchPhase =
  | 'Placing'
  | 'Bidding'
  | 'Revealing'
  | 'DiscardChoice'
  | 'RoundOver'
  | 'Finished'

export type DiscType = 'Rose' | 'Skull'

export interface DiscSlot {
  type: DiscType
  flipped: boolean
}

export interface FlipLog {
  flippedByPlayerId: string
  stackOwnerId: string
  result: DiscType
  flipNumber: number
}

export interface DevicePlayer {
  id:          string
  displayName: string
  avatarUrl:   string | null
  seatIndex:   number
  stack:       DiscSlot[]   // projected: opponent stacks arrive as []
  stackCount:  number       // always correct; use this to render face-down discs
  rosesOwned:  number
  skullOwned:  boolean
  pointsWon:   number
  active:      boolean
  passed:      boolean
}

export interface DeadMansSwitchState {
  phase:                     DeadMansSwitchPhase
  players:                   DevicePlayer[]
  currentPlayerIndex:        number
  currentBid:                number
  totalDiscsOnTable:         number
  challengerId:              string | null
  nextRoundFirstPlayerIndex: number
  lastFlip:                  FlipLog | null
  winner:                    string | null
  roundNumber:               number
}
```

---

## Actions

All actions use the flat record pattern matching Liar's Dice. Optional payload fields are `null` when not applicable.

| Type | `TargetCount?` | `NewBid?` | `TargetPlayerId?` | `DiscType?` | Who | When |
|---|---|---|---|---|---|---|
| `StartGame` | — | — | — | — | Host | Room `waiting` |
| `PlaceDisc` | — | — | — | — | Current player | `Placing` |
| `StartBid` | bid number | — | — | — | Current player (placed ≥1 disc) | `Placing` |
| `RaiseBid` | — | new bid | — | — | Current non-passed player | `Bidding` |
| `Pass` | — | — | — | — | Current non-passed player | `Bidding` |
| `FlipDisc` | — | — | target player ID | — | Challenger | `Revealing` |
| `DiscardDisc` | — | — | — | `Rose` or `Skull` | Challenger | `DiscardChoice` |
| `StartNextRound` | — | — | — | — | Any active player | `RoundOver` |

**C# action record:**

```csharp
public record DeadMansSwitchAction(
    string  Type,
    int?    TargetCount    = null,
    int?    NewBid         = null,
    string? TargetPlayerId = null,
    DiscType? DiscType     = null
);
```

---

## Projection behaviour

The module implements `HasStateProjection = true`. `ProjectForPlayer(state, playerId)` transforms state before it is sent to each client.

| Phase | `Stack` for own player | `Stack` for opponents | `StackCount` | Notes |
|---|---|---|---|---|
| `Placing` | Full (`Type` visible, `Flipped = false`) | `[]` | Preserved (accurate count) | Opponent cannot deduce whether skull is placed |
| `Bidding` | Full | `[]` | Preserved | Same as Placing |
| `Revealing` | Full | `[]` — except `Flipped = true` discs, which are included with `Type` visible | Preserved | Flipped discs become visible to all as they are revealed; `LastFlip` also carries the result |
| `DiscardChoice` | Full | `[]` with flipped discs visible | Preserved | Only Challenger may act |
| `RoundOver` | Full state visible to all | Full state visible to all | Preserved | All disc types revealed for post-round inspection |
| `Finished` | Full state visible to all | Full state visible to all | Preserved | Game over; full visibility |

**Own-stack-first validation:** During `Revealing`, the server rejects `FlipDisc` targeting any opponent stack unless the Challenger's own `Stack` contains zero unflipped discs. Rejection reason: `"You must flip your own devices first."`

**Auto-discard logic:** During `DiscardChoice`, if `RosesOwned == 0` or `SkullOwned == false` (i.e. the Challenger owns only one disc type), the server auto-discards the only available type without waiting for a `DiscardDisc` action and immediately transitions to `RoundOver`.

---

## Acceptance criteria

### Setup
- [ ] Given a room with 3–6 players, when the host sends `StartGame`, then each player's state has `rosesOwned: 3`, `skullOwned: true`, `stack: []`, `stackCount: 0`, `pointsWon: 0`, `active: true`, and the phase is `Placing` with `currentPlayerIndex: 0` (seat 0 leads round 1).
- [ ] Given a room with fewer than 3 or more than 6 players, when the host sends `StartGame`, then the action is rejected with a clear reason and the game does not begin.

### Placing phase
- [ ] Given the `Placing` phase and it is player A's turn, when player A sends `PlaceDisc`, then a disc is added face-down to the top of player A's stack, `stackCount` increments, `totalDiscsOnTable` increments, and the turn advances to the next active player.
- [ ] Given the `Placing` phase and it is not player A's turn, when player A sends `PlaceDisc`, then the action is rejected and the state is unchanged.
- [ ] Given the `Placing` phase and player A has `stackCount == 0` this round, when player A sends `StartBid`, then the action is rejected with reason "You must arm at least one device before committing to the job."
- [ ] Given the `Placing` phase and player A has `stackCount >= 1`, when player A sends `StartBid` with `targetCount` between 1 and `totalDiscsOnTable` inclusive, then the phase transitions to `Bidding`, `currentBid` equals `targetCount`, `challengerId` is set to player A's ID, and the bidding rotation starts with player A.
- [ ] Given the `Placing` phase and player A sends `StartBid` with `targetCount` of 0 or greater than `totalDiscsOnTable`, then the action is rejected.

### Bidding phase
- [ ] Given the `Bidding` phase and it is player B's turn and `currentBid` is N, when player B sends `RaiseBid` with `newBid` of N+1 or higher (up to `totalDiscsOnTable`), then `currentBid` updates, the turn advances to the next non-passed active player, and all clients receive the updated state.
- [ ] Given the `Bidding` phase and it is player B's turn, when player B sends `RaiseBid` with `newBid` less than or equal to `currentBid`, then the action is rejected.
- [ ] Given the `Bidding` phase and it is player B's turn, when player B sends `Pass`, then player B has `passed: true` and the turn advances to the next non-passed active player.
- [ ] Given the `Bidding` phase and only one player has not passed, then that player becomes the Challenger automatically and the phase transitions to `Revealing` without requiring a player action.
- [ ] Given the `Bidding` phase and a player raises `newBid` to equal `totalDiscsOnTable`, then that player immediately becomes the Challenger and the phase transitions to `Revealing`.

### Revealing phase — own-stack constraint
- [ ] Given the `Revealing` phase and the Challenger still has unflipped discs in their own stack, when the Challenger sends `FlipDisc` with `targetPlayerId` equal to their own ID, then the top unflipped disc of their stack is flipped, `lastFlip` is set, and the flipped count increments.
- [ ] Given the `Revealing` phase and the Challenger still has unflipped discs in their own stack, when the Challenger sends `FlipDisc` targeting any opponent's stack, then the action is rejected with "You must flip your own devices first."

### Revealing phase — success
- [ ] Given the `Revealing` phase and the Challenger flips the Nth non-skull disc where N equals `currentBid`, then the Challenger's `pointsWon` increments, the phase transitions to `RoundOver`, and `nextRoundFirstPlayerIndex` is unchanged (the round's bid-opener leads the next round).
- [ ] Given a Challenger's `pointsWon` reaches 2 on a successful reveal, then the phase transitions to `Finished`, `winner` is set to that player's ID, and a `GameOverEffect` is emitted.

### Revealing phase — failure (own skull)
- [ ] Given the `Revealing` phase and the Challenger flips a disc from their own stack that is a skull, then the phase transitions to `DiscardChoice` and only the Challenger may send `DiscardDisc`.
- [ ] Given the `DiscardChoice` phase and the Challenger owns at least one rose and still owns their skull (both disc types available), when the Challenger sends `DiscardDisc` with `discType: 'Rose'` or `discType: 'Skull'`, then that disc type count decrements permanently, `nextRoundFirstPlayerIndex` is set to the Challenger's index, and the phase transitions to `RoundOver`.
- [ ] Given the `DiscardChoice` phase and the Challenger owns only one disc type (e.g. `rosesOwned == 0` or `skullOwned == false`), then the server auto-discards that type immediately without waiting for `DiscardDisc`, `nextRoundFirstPlayerIndex` is set to the Challenger's index, and the phase transitions to `RoundOver`.
- [ ] Given the Challenger sends `DiscardDisc` with `discType: 'Rose'` but `rosesOwned == 0`, then the action is rejected.
- [ ] Given the Challenger sends `DiscardDisc` with `discType: 'Skull'` but `skullOwned == false`, then the action is rejected.

### Revealing phase — failure (opponent skull)
- [ ] Given the `Revealing` phase and the Challenger flips a disc from an opponent's stack (owner = player C) that is a skull, then the server immediately picks one of the Challenger's remaining discs at random to permanently remove, `nextRoundFirstPlayerIndex` is set to player C's index, and the phase transitions to `RoundOver` — no additional player action is required.

### Elimination and win by last active player
- [ ] Given a player's total remaining discs (`rosesOwned + (skullOwned ? 1 : 0)`) reaches 0 after a discard, then that player's `active` flag is set to `false` and they receive full state visibility for the rest of the game.
- [ ] Given only one player remains `active`, then the phase transitions to `Finished`, `winner` is set to that player's ID, and a `GameOverEffect` is emitted without requiring a reveal.

### Round reset
- [ ] Given the phase is `RoundOver`, when any active player sends `StartNextRound`, then all active players have their stacks cleared and their `stack` / `stackCount` reset to empty, `rosesOwned` and `skullOwned` remain at their current (post-discard) values, the phase returns to `Placing`, and `currentPlayerIndex` is set to `nextRoundFirstPlayerIndex`.

### Projection
- [ ] Given any player other than player A receives state during `Placing` or `Bidding`, then player A's `stack` field in that state is `[]` and `stackCount` reflects the true disc count.
- [ ] Given the `Revealing` phase and a disc in opponent player A's stack has `flipped: true`, then all players' projected state includes that disc entry with `type` visible.
- [ ] Given the phase is `RoundOver` or `Finished`, then all players receive the full unredacted state including all disc types in all stacks.

---

## Architecture decisions

- **IGameModule + IGameHandler directly (not ReducerGameModule):** `ReducerGameModule` wraps the handler and cannot emit `GameOverEffect`. Dead Man's Switch requires `GameOverEffect` in two places (Challenger reaches 2 points; last active player wins). Using the base interfaces directly gives full control over `GameResult` construction.
- **`DiscardDisc` uses `DiscType` enum, not a hand index:** The Challenger always knows their own hand composition. `DiscType` (`Rose` | `Skull`) is unambiguous in all legal states because a player can only discard a type they currently own. The server validates ownership before applying.
- **Auto-discard when only one disc type remains:** Eliminates a degenerate wait state where the Challenger is forced to send a `DiscardDisc` for the only possible choice. The server applies the discard immediately and advances the phase.
- **Server picks randomly on opponent skull hit (no `OpponentDiscardChoice` phase):** Removes interactive latency after a skull hit. The published Skull rules describe the opponent's discard as a random pick; keeping it random is both faithful to the rules and simpler to implement. `OpponentDiscardOwnerId` state field and `ChooseDiscardForChallenger` action are not needed.
- **`ChooseFirstPlayer` action removed:** Next round's first player is fully deterministic. Own skull hit → Challenger leads. Opponent skull hit → skull owner leads. Successful reveal → bid-opener (round 1: seat 0) leads. No player choice required.
- **`StartNextRound` available to any active player:** Consistent with the principle that any active participant can advance the game between rounds, not only the host.
- **No `Hidden` sentinel on `DiscType`:** Opponent stacks are projected as empty arrays (`[]`). `StackCount` carries the visible count. This keeps `DiscType` to two values (`Rose`, `Skull`) and avoids optional type fields in the TypeScript mirror.
- **Empty `DbContext` + initial migration scaffolded from the start:** Consistent with the Liar's Dice precedent. Uses `MigrationsHistoryTable("__EFMigrationsHistory_deadmansswitch")`.
- **`[JsonConverter(typeof(JsonStringEnumConverter))]` on every enum:** `DeadMansSwitchPhase` and `DiscType` both require this decorator. Without it enums serialize as integers and TypeScript string unions will never match.

---

## Out of scope

- Undo (`SupportsUndo = false` — bluffing games with revealed information cannot be meaningfully undone)
- Async / pass-and-play mode (`SupportsAsync = false`)
- Spectator chat
- Persistent leaderboards or cross-room win records
- Sound effects or haptic feedback
- Custom room options at creation (v1 uses fixed starting hand: 3 roses + 1 skull)
- Any form of disc-ordering within a hand (players place discs by type; the server tracks rose count and skull ownership, not individual disc identities)

---

## Implementation hints

- **Backend:** `{agent: backend}` — implement `IGameModule` and `IGameHandler` directly; set `HasStateProjection = true`; emit `GameOverEffect` in two places (2-point win and last-active-player win); implement `ProjectForPlayer`; scaffold empty `DeadMansSwitchDbContext` with initial migration using `MigrationsHistoryTable("__EFMigrationsHistory_deadmansswitch")`.
- **Frontend:** `{agent: frontend, ux}` — consume `GameContext<DeadMansSwitchState>`; render face-down discs using `stackCount` (not `stack.length`); reveal animation on `lastFlip`; phase-aware action panel; `data-game-theme="deadmansswitch"` on game root.
- **Registry:** Add `deadmansswitch` entry to `apps/frontend/src/games/registry.ts`.
- **CI:** `{agent: devops}` — ensure the empty initial migration runs cleanly in the CI pipeline migration step.
