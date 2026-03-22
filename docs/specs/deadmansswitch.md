# Feature: Dead Man's Switch game module

**Story:** story-030
**Status:** Draft — Round 1 (analyst). Pending `/spec-design` architect response.
**Date:** 2026-03-22
**Game ID:** `deadmansswitch`

---

## 1. Problem statement

Meepliton's two live game modules (Skyline, Liar's Dice) cover tile placement and dice-based hidden bidding. The group needs a fast party game that plays in 20–30 minutes, supports 3–6 players, and rewards social reading rather than calculation. Skull (the published board game) fills that gap exactly, but the platform has no bluffing game with a disc-placement mechanic.

Beyond the entertainment gap: Dead Man's Switch exercises two platform capabilities that neither existing game has used together:
1. Per-player hidden disc ordering (which disc is on top of each stack is known only to the owner) — a stricter form of hidden state than Liar's Dice dice.
2. A multi-phase turn structure with a hand-off mid-round (one player triggers bidding; a different player becomes the Challenger). This will stress-test the `currentPlayerIndex` / turn routing on the frontend and the phase-transition logic in `ReducerGameModule`.

---

## 2. Proposed solution

A self-contained C# game module (`Meepliton.Games.DeadMansSwitch`) and matching React module (`apps/frontend/src/games/deadmansswitch/`), scaffolded with `scripts/new-game.ps1` and registered in `registry.ts`.

### Theme

Heist / bomb-defusal. Language throughout:
- Discs = "devices"
- Skull disc = "trigger"
- Rose discs = "duds"
- Placing a disc = "arming a device"
- Opening a bid = "committing to the job"
- The bid number = "target count"
- Winning a reveal = "defusing the switch"
- Player mat = "mission board"

The in-game room view carries this theme via `data-game-theme="deadmansswitch"` on the game root element, scoping CSS token overrides. The lobby, header, and waiting screen remain in the platform default theme (per ADR-008 and the Liar's Dice precedent).

### Rules implemented

3–6 players. Each player starts with 4 disc tokens on their mat: 3 roses + 1 skull.

**Phase 1 — Placing**
In seat order, each player must either:
- Place one disc face-down on top of their stack (action: `PlaceDisc`), OR
- Open bidding by committing to a target count, provided they have already placed at least one disc (action: `StartBid`)

On the first time through the table, every player must place before anyone can open bidding. After that first pass any player whose turn it is may open bidding instead of placing.

**Phase 2 — Bidding**
The player who opened bidding sets the opening number. Going around in seat order, each player must either raise the bid (action: `RaiseBid`) or pass (action: `Pass`). A player who has passed is skipped for the rest of the bidding phase. The last player still in (everyone else has passed) becomes the Challenger. If a player raises the bid to the total number of all face-down discs currently on the table, bidding ends immediately and they become the Challenger.

**Phase 3 — Revealing**
The Challenger flips discs one at a time. They must flip all their own discs (top-to-bottom, i.e. most-recently-placed first) before touching anyone else's stacks. After their own stack is cleared, they freely choose which player's stack to draw from next (action: `FlipDisc`). Each flip is an individual action so all clients see the result in real time before the next flip.

- If the flipped count reaches the bid number without hitting a skull: **success**.
- If a skull is flipped: **failure** — reveal stops immediately.

**Success:** Challenger earns 1 point (their mat flips). First player to reach 2 points wins.

**Failure:**
- Own skull: Challenger permanently removes one disc of their choice from the game (`DiscardDisc` action). They choose the first player of the next round.
- Opponent's skull: The skull's owner chooses at random which of the Challenger's discs to discard. The skull's owner is the first player of the next round.

**Elimination:** A player with 0 discs is eliminated and spectates for the rest of the game. If only one player remains active, that player wins.

**Round reset:** All surviving players reclaim their remaining (non-discarded) discs. All stacks are cleared. New round begins in `Placing` phase from the designated first player.

---

## 3. User stories

- As a player, I want to arm devices face-down so that my opponents cannot see whether I placed my trigger.
- As a player with at least one device armed, I want to commit to the job by setting a target count so that I can force a reveal that I believe I can win.
- As a player in the bidding phase, I want to raise the target or pass so that I can control whether I become the Challenger.
- As the Challenger, I want to flip my own devices first so that I can protect myself before exposing myself to opponent triggers.
- As the Challenger, I want to freely choose which opponent's stack to draw from so that I can play the odds on who I trust.
- As an eliminated player, I want to stay in the room and watch so that I can follow the rest of the mission.
- As any player, I want to see the current bid, who has passed, and whose stack has how many devices so that I can make informed decisions.

---

## 4. Acceptance criteria

### Setup
- [ ] Given a room with 3–6 players, when the host sends `StartGame`, then each player's state has `roses: 3`, `skull: 1` (all unplaced), their stack is empty, and the phase is `Placing`.
- [ ] Given a room with fewer than 3 or more than 6 players, when the host sends `StartGame`, then the action is rejected with a clear reason and the game does not begin.

### Placing phase
- [ ] Given the `Placing` phase and it is player A's turn, when player A sends `PlaceDisc`, then a disc is added face-down to the top of player A's stack, the turn advances to the next active player, and the total disc count on the table increases by 1.
- [ ] Given the `Placing` phase and it is not player A's turn, when player A sends `PlaceDisc`, then the action is rejected and the state is unchanged.
- [ ] Given the `Placing` phase and player A has not yet placed any disc this round, when player A sends `StartBid`, then the action is rejected (must place before opening bidding).
- [ ] Given the `Placing` phase and player A has placed at least one disc, when player A sends `StartBid` with a target count between 1 and the total discs on the table (inclusive), then the phase transitions to `Bidding`, the opening bid equals the target count, and player A is recorded as the opening bidder.
- [ ] Given the `Placing` phase and player A sends `StartBid` with a target count of 0 or greater than the total discs on the table, then the action is rejected.

### Bidding phase
- [ ] Given the `Bidding` phase and it is player B's turn and the current bid is N, when player B sends `RaiseBid` with a value of N+1 or higher (up to total discs on table), then the bid updates, the turn advances to the next non-passed active player, and all clients receive the updated state.
- [ ] Given the `Bidding` phase and it is player B's turn, when player B sends `RaiseBid` with a value less than or equal to the current bid, then the action is rejected.
- [ ] Given the `Bidding` phase and it is player B's turn, when player B sends `Pass`, then player B is marked as passed, is skipped for the rest of bidding, and the turn advances to the next non-passed active player.
- [ ] Given the `Bidding` phase and only one player has not passed, then that player becomes the Challenger automatically (no action required) and the phase transitions to `Revealing`.
- [ ] Given the `Bidding` phase and a player raises the bid to equal the total number of face-down discs on the table, then that player immediately becomes the Challenger and the phase transitions to `Revealing`.

### Revealing phase — own discs
- [ ] Given the `Revealing` phase and the Challenger still has unflipped discs in their own stack, when the Challenger sends `FlipDisc` targeting their own stack, then the top disc of their stack is flipped, if it is a rose the flipped count increments and the Challenger may continue, if it is the skull the reveal fails immediately and the failure path begins.
- [ ] Given the `Revealing` phase and the Challenger sends `FlipDisc` targeting any opponent's stack before their own stack is fully flipped, then the action is rejected.

### Revealing phase — success
- [ ] Given the `Revealing` phase and the Challenger flips the Nth non-skull disc (where N equals the bid), then the Challenger gains 1 point (their `pointsWon` increments to 1 or 2), the round ends, and the state transitions to `RoundOver` pending the host's `StartNextRound` action.
- [ ] Given a player has `pointsWon === 2`, when the round ends with their success, then the phase transitions to `Finished`, `winner` is set to that player's ID, and a `GameOverEffect` is emitted.

### Revealing phase — failure (own skull)
- [ ] Given the `Revealing` phase and the Challenger flips their own skull, then the phase transitions to `DiscardChoice` and the Challenger (and only the Challenger) must send `DiscardDisc` naming one of their remaining discs to permanently remove.
- [ ] Given the `DiscardChoice` phase, when the Challenger sends `DiscardDisc` naming a valid disc, then that disc is permanently removed from the game, the Challenger also chooses the next round's first player via `ChooseFirstPlayer`, and the round transitions to `RoundOver`.
- [ ] Given the Challenger sends `DiscardDisc` naming a disc they do not own or that is already discarded, then the action is rejected.

### Revealing phase — failure (opponent's skull)
- [ ] Given the `Revealing` phase and the Challenger flips an opponent's skull (skull owner = player C), then the phase transitions to `OpponentDiscardChoice` and only player C may send `ChooseDiscardForChallenger`, naming one of the Challenger's remaining discs to permanently remove at random (the spec: player C's selection is their "random" pick from the Challenger's discs).
- [ ] Given the `OpponentDiscardChoice` phase and player C sends `ChooseDiscardForChallenger` naming a valid disc from the Challenger's hand, then that disc is removed permanently, player C is recorded as next round's first player, and the round transitions to `RoundOver`.

### Elimination and win by last player
- [ ] Given a player's total disc count (roses + skull remaining) reaches 0, then that player's `active` flag is set to `false`, they are excluded from further play, and they receive full state visibility for the rest of the game.
- [ ] Given only one player remains `active`, then the phase transitions to `Finished`, `winner` is set to that player's ID, and a `GameOverEffect` is emitted without requiring a reveal.

### Round reset
- [ ] Given the phase is `RoundOver`, when the host sends `StartNextRound`, then all active players reclaim their non-discarded discs (stacks cleared, hand restored to remaining disc count), the phase returns to `Placing`, and the designated first player is set as `currentPlayerIndex`.

---

## 5. State shape (proposed — for architect review)

### C# records

```csharp
// Enums — all require [JsonConverter(typeof(JsonStringEnumConverter))]
public enum DeadMansSwitchPhase
{
    Placing,
    Bidding,
    Revealing,
    DiscardChoice,        // Challenger failed on own skull; awaiting Challenger's discard pick
    OpponentDiscardChoice, // Challenger failed on opponent skull; awaiting opponent's discard pick
    RoundOver,
    Finished
}

public record DeadMansSwitchState(
    DeadMansSwitchPhase Phase,
    List<DevicePlayer>  Players,
    int                 CurrentPlayerIndex,   // whose turn it is (Placing/Bidding/Revealing/DiscardChoice/OpponentDiscardChoice)
    int                 CurrentBid,           // 0 when not in Bidding/Revealing
    int                 TotalDiscsOnTable,    // sum of all stack counts (cached for bid validation)
    string?             ChallengerId,         // set when phase enters Bidding
    string?             OpponentDiscardOwnerId, // skull owner in OpponentDiscardChoice phase
    int                 NextRoundFirstPlayerIndex,
    FlipLog?            LastFlip,             // last disc flipped (for animation / result display)
    string?             Winner,               // userId, set in Finished phase
    int                 RoundNumber
);

public record DevicePlayer(
    string      Id,
    string      DisplayName,
    string?     AvatarUrl,
    int         SeatIndex,
    int         RosesInHand,    // unplaced roses remaining
    bool        SkullInHand,    // whether their skull is still unplaced
    List<DiscSlot> Stack,       // ordered top-to-bottom; only owner sees disc type
    int         StackCount,     // always == Stack.Count (cached for display)
    int         RosesTotal,     // permanent rose count (decrements on discard)
    bool        SkullOwned,     // permanent skull ownership (false if skull was discarded)
    int         PointsWon,      // 0, 1, or 2
    bool        Active,         // false = eliminated
    bool        Passed          // in Bidding phase, did this player pass?
);

public record DiscSlot(
    DiscType Type,   // Rose or Skull
    bool     Flipped // true after the Challenger flips it
);

public enum DiscType { Rose, Skull }

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
  | 'OpponentDiscardChoice'
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
  id:              string
  displayName:     string
  avatarUrl:       string | null
  seatIndex:       number
  rosesInHand:     number
  skullInHand:     boolean
  stack:           DiscSlot[]   // projected: other players see DiscSlot with type hidden
  stackCount:      number
  rosesTotal:      number
  skullOwned:      boolean
  pointsWon:       number
  active:          boolean
  passed:          boolean
}

export interface DeadMansSwitchState {
  phase:                     DeadMansSwitchPhase
  players:                   DevicePlayer[]
  currentPlayerIndex:        number
  currentBid:                number
  totalDiscsOnTable:         number
  challengerId:              string | null
  opponentDiscardOwnerId:    string | null
  nextRoundFirstPlayerIndex: number
  lastFlip:                  FlipLog | null
  winner:                    string | null
  roundNumber:               number
}
```

### Hidden information strategy

Dead Man's Switch requires the same projection mechanism used by Liar's Dice (`HasStateProjection = true`). Two categories of hidden information:

1. **Disc type in stacks (all phases):** Each `DiscSlot.Type` in another player's stack must be hidden until the Challenger flips it. The server projects each player's view so that unflipped `DiscSlot` entries in opponent stacks have their `Type` replaced with a sentinel (e.g. a third enum value `Hidden`, or the field entirely removed). The `StackCount` remains visible so players can track how many devices are armed.

2. **Discs in hand:** A player's `RosesInHand` and `SkullInHand` are visible only to themselves. Other players see only `StackCount`. This prevents deduction of whether a player has placed their skull yet.

The projection logic should be implemented in `DeadMansSwitchModule.ProjectForPlayer`, following the `LiarsDiceModule` pattern. During `Revealing` phase, flipped discs become visible to all (their `Type` is revealed as part of the `FlipLog`).

---

## 6. Actions (proposed)

| Type | Payload | Who | When |
|---|---|---|---|
| `StartGame` | — | Host | Room in `waiting` status |
| `PlaceDisc` | — | Current player | `Placing` phase |
| `StartBid` | `targetCount: number` | Current player (must have placed ≥1 disc) | `Placing` phase |
| `RaiseBid` | `newBid: number` | Current non-passed player | `Bidding` phase |
| `Pass` | — | Current non-passed player | `Bidding` phase |
| `FlipDisc` | `targetPlayerId: string` | Challenger | `Revealing` phase |
| `DiscardDisc` | `discType: DiscType` | Challenger | `DiscardChoice` phase |
| `ChooseFirstPlayer` | `playerId: string` | Challenger | After `DiscardDisc` in `DiscardChoice` phase |
| `ChooseDiscardForChallenger` | `discType: DiscType` | Skull owner | `OpponentDiscardChoice` phase |
| `StartNextRound` | — | Host | `RoundOver` phase |

---

## 7. API changes

None. All interaction flows through the existing `SendAction` / `StateUpdated` SignalR contract. No new endpoints required.

---

## 8. Data model changes

No supplementary tables needed for v1. Game state lives entirely in `rooms.game_state` (JSONB). `DeadMansSwitchDbContext` will be scaffolded but will contain only the read-only platform views — no game-owned tables. The migration will be an empty initial migration.

---

## 9. Theme specification

The heist / bomb-defusal theme applies only to the in-game room view.

The room root sets `data-game-theme="deadmansswitch"`. Token overrides are scoped to that selector.

Aesthetic direction:
- Black-ops / industrial — very dark near-black backgrounds, muted steel surfaces
- Accent: sharp amber-yellow for active/interactive elements (danger signal without being fire-engine red)
- Typography: tight uppercase sans-serif for labels; monospaced numerals for counts and bids
- Avoid: military-cliché greens, cartoon explosions, neon

Suggested CSS token overrides (for architect / UX review):

```css
[data-game-theme="deadmansswitch"] {
  --color-background:     #0a0a0c;   /* near black */
  --color-surface:        #121218;   /* dark steel */
  --color-surface-raised: #1c1c26;
  --color-surface-hover:  #22222e;
  --color-primary:        #d4a017;   /* amber warning */
  --color-on-primary:     #0a0a0c;
  --color-border:         #2a2a38;
  --color-text:           #d8d8e0;   /* cold white */
  --color-text-muted:     #6a6a80;
  --radius-sm:            2px;
  --radius-md:            3px;       /* sharp and precise */
}
```

---

## 10. Out of scope

- Undo (`SupportsUndo = false` — bluffing games with revealed information cannot be meaningfully undone)
- Async / pass-and-play mode (`SupportsAsync = false`)
- Spectator chat
- Persistent leaderboards or cross-room win records
- Sound effects or haptic feedback
- Any per-game custom options at room creation (v1 uses fixed starting hand of 3 roses + 1 skull)

---

## 11. Open questions (for architect response)

- **OQ-DMS-01** (blocking): The `DiscardDisc` action requires the Challenger to name which of their remaining discs to remove. When it is their own skull failure, the failure says "Challenger chooses which disc to discard." Because a player always knows their own hand composition, this choice is meaningful. However, the projection means the server knows the true disc types. Should `DiscardDisc` take a `DiscType` enum value, or an index into the hand? Using `DiscType` is simpler but could be ambiguous if a player has multiple roses; an index is unambiguous. Recommendation: use index into the ordered hand (roses first, skull last), but needs architect sign-off.

- **OQ-DMS-02** (blocking): The `ChooseDiscardForChallenger` action says the skull owner picks one of the Challenger's discs "at random." The rules state the opponent selects randomly, but the spec makes it an interactive choice. Two interpretations: (A) the server picks randomly and sends the result — no player action needed, or (B) the skull owner actively picks (which is how some groups play). Recommendation: server picks randomly (interpretation A) so there is no waiting on the skull owner to act. This makes `ChooseDiscardForChallenger` unnecessary and collapses the `OpponentDiscardChoice` phase. Needs product decision — could go either way.

- **OQ-DMS-03** (non-blocking): The `FlipDisc` action for the Challenger's own stack could be auto-sequenced by the server (server automatically flips each disc top-to-bottom on each action) or require an explicit `FlipDisc` per disc targeting self. The spec calls for one action per flip so all clients see the result in real time. This is the better UX but is worth confirming.

- **OQ-DMS-04** (non-blocking): Should the `DiscSlot.Type` for hidden discs be omitted from the projected JSON entirely, or replaced with a `Hidden` sentinel value? Omitting the field is cleaner but requires the TypeScript type to make `type` optional (`type?: DiscType`), which adds null guards everywhere in the frontend. A `Hidden` sentinel on the enum avoids optional fields. Recommendation: add `Hidden` as a third `DiscType` value, used only in projected state.

- **OQ-DMS-05** (non-blocking): First round first player — the rules say the first player of the first round is determined by group convention (often "whoever most recently bluffed" or simply the host). Recommendation: host is seat 0 / first in seat order for round 1. Subsequent rounds use the skull-owner or Challenger-choice rule. Confirm or override.

- **OQ-DMS-06** (non-blocking): Bidding opener — when player A calls `StartBid` with a target count, do they also take the first position in the bidding rotation (so it goes A → B → C → ... → A) or do they skip themselves and bidding starts from the next player? The published Skull rules have the opener start bidding and others raise or pass in turn. Recommendation: opener starts and is themselves in the rotation — they set the opening number and may raise again if it comes back to them. Confirm.

- **OQ-DMS-07** (non-blocking): `RoundOver` vs auto-advance — should the host be required to explicitly send `StartNextRound`, or should the server auto-advance after a fixed delay (e.g. 5 seconds)? Consistent with the Liar's Dice decision (OQ-LD-02): require explicit action. Players need time to absorb the result and the disc discard interaction must complete cleanly before the round resets.
