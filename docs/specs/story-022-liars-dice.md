# Feature: Liar's Dice game module

**Story:** story-022
**Status:** Draft — ready for `/story-review`
**Date:** 2026-03-14

---

## Summary

Liar's Dice is a dice-based bluffing game for 2–6 players. Each player keeps their dice hidden under a cup; players take turns making ascending bids on how many dice of a given face value exist across all cups. Any player may challenge the current bid by calling "Liar" — dice are revealed and the loser drops one die. The last player with any dice wins. This is the second Meepliton game module, chosen to validate the module system against a game that is structurally unlike Skyline: it has hidden information, player elimination, and round-level rule variation.

---

## User stories

- As a player, I want to see only my own dice so that bluffing is possible.
- As a player on my turn, I want to raise the current bid or call Liar so that I can play strategically.
- As a player who calls Liar, I want all dice revealed and the result resolved immediately so that the round ends decisively.
- As a player who just reached one die, I want the option to call Palifico so that the round plays differently.
- As an eliminated player, I want to stay in the room and watch the rest of the game.
- As a spectator or eliminated player, I want to see all dice after a reveal so that I can follow the game.
- As the host, I want to start the game when all players are ready.

---

## Game rules

### Setup
- 2–6 players. Default starting dice per player: 5 (configurable at room creation, range 3–7).
- On each new round, every active player rolls their full current set of dice. Dice are hidden from other players.
- The player who lost a die in the previous round bids first (or the host for round 1).

### Turn structure
On each turn the current player must do exactly one of:
1. **Raise the bid:** Declare `(quantity, face)` where the new bid is strictly higher than the current bid. A bid `(q2, f2)` is higher than `(q1, f1)` when:
   - `q2 > q1` (any face), OR
   - `q2 == q1` and `f2 > f1`
2. **Call Liar:** Challenge the previous player's bid.

The first player in a round may not call Liar (there is no previous bid to challenge). They must open with any valid bid.

### Resolving a Liar call
All dice are revealed. Count how many dice show the bid face (plus 1s if wild — see below).
- If the count is **greater than or equal to** the bid quantity: the **challenger** (caller) loses one die.
- If the count is **less than** the bid quantity: the **bidder** (previous player) loses one die.

After resolution, a new round begins. The player who lost a die bids first.

### Wilds (1s)
- In a normal round, dice showing **1** count as wild: they count toward any face bid.
- Exception: if the current bid is specifically for **face 1**, then 1s are NOT wild — only literal 1s count.

### Palifico round
- When a player's die count drops to **exactly 1**, before the new round's first bid they may declare Palifico.
- In a Palifico round, **1s are not wild** for any bid. All dice count only at face value.
- A player may declare Palifico at most once per game (tracked in state).
- If the player with one die does not declare Palifico, the round plays as normal.

### Elimination and spectating
- A player who loses their last die is **eliminated** and becomes a spectator.
- Eliminated players see the full game state (all dice, all history) and may chat but cannot act.
- The game ends when only one player has dice remaining.

### Game end
- The surviving player wins. A `GameOverEffect(winnerId)` is emitted.
- Phase transitions to `finished`.

---

## State shape

### C# records (`LiarsDiceModels.cs`)

```csharp
public record LiarsDiceState(
    LiarsDicePhase  Phase,
    List<DicePlayer> Players,
    int              CurrentPlayerIndex,
    Bid?             CurrentBid,
    int              RoundNumber,
    bool             PalificoActive,      // is this a Palifico round?
    string?          LastChallengeResult, // human-readable result shown post-reveal
    RevealSnapshot?  LastReveal,          // populated during reveal phase, null otherwise
    string?          Winner               // userId, set in finished phase
);

public record DicePlayer(
    string       Id,
    string       DisplayName,
    string?      AvatarUrl,
    int          SeatIndex,
    List<int>    Dice,          // each element is 1–6; empty when eliminated
    int          DiceCount,     // always == Dice.Count for active players; 0 when eliminated
    bool         Active,        // false = eliminated
    bool         HasUsedPalifico
);

public record Bid(int Quantity, int Face);

public record RevealSnapshot(
    List<PlayerReveal> Players,
    Bid                ChallengedBid,
    int                ActualCount,   // dice matching the bid (accounting for wilds if applicable)
    string             LoserId       // userId of the player who lost a die
);

public record PlayerReveal(string PlayerId, List<int> Dice);

public enum LiarsDicePhase
{
    Bidding,   // normal bidding in progress
    Reveal,    // Liar called; dice visible; result calculated; waiting for NextRound action
    Finished   // game over
}
```

**Key constraints enforced by Validate:**
- `Bidding`: only `CurrentPlayerIndex`'s player may act; actions are `Bid` or `CallLiar`
- `Reveal`: only `StartNextRound` is accepted (host-initiated or auto-advance)
- `Finished`: all actions rejected
- `StartGame`: only accepted when phase would be pre-game (pre-game represented by a separate startup action; see below)

### TypeScript mirror (`types.ts`)

```typescript
export type LiarsDicePhase = 'Bidding' | 'Reveal' | 'Finished'

export interface LiarsDiceState {
  phase:               LiarsDicePhase
  players:             DicePlayer[]
  currentPlayerIndex:  number
  currentBid:          Bid | null
  roundNumber:         number
  palificoActive:      boolean
  lastChallengeResult: string | null
  lastReveal:          RevealSnapshot | null
  winner:              string | null
}

export interface DicePlayer {
  id:              string
  displayName:     string
  avatarUrl:       string | null
  seatIndex:       number
  dice:            number[]   // empty array for eliminated players
  diceCount:       number
  active:          boolean
  hasUsedPalifico: boolean
}

export interface Bid {
  quantity: number
  face:     number
}

export interface RevealSnapshot {
  players:       PlayerReveal[]
  challengedBid: Bid
  actualCount:   number
  loserId:       string
}

export interface PlayerReveal {
  playerId: string
  dice:     number[]
}
```

---

## Actions

### C# action discriminated union (`LiarsDiceAction`)

```csharp
public record LiarsDiceAction(
    string    Type,
    BidPayload?  BidData    = null,
    bool?     DeclarePalifico = null
);

public record BidPayload(int Quantity, int Face);
```

| `Type` | Payload | Who | When |
|---|---|---|---|
| `StartGame` | — | Host only | Room is in `waiting` status |
| `PlaceBid` | `BidData: { quantity, face }` | Current player | `Bidding` phase |
| `CallLiar` | — | Current player | `Bidding` phase, not first bid of round |
| `StartNextRound` | — | Any active player | `Reveal` phase |
| `DeclarePalifico` | — | Current player with exactly 1 die | First action of a new round, before any bid |

### TypeScript action union

```typescript
export type LiarsDiceAction =
  | { type: 'StartGame' }
  | { type: 'PlaceBid';       bid: Bid }
  | { type: 'CallLiar' }
  | { type: 'StartNextRound' }
  | { type: 'DeclarePalifico' }
```

---

## Hidden information strategy

Liar's Dice requires each player to see only their own dice during the `Bidding` phase.

**Decision: server-side state projection via `LiarsDiceModule.ProjectForPlayer`.**

`LiarsDiceModule` overrides `ProjectForPlayer` in its `ReducerGameModule` base class and sets `HasStateProjection = true`. The platform's `GameDispatcher` detects this and sends each player a projected copy of the state rather than broadcasting the full state to the room group. During `Bidding`, a player receives their own `dice` array intact; all other players' `dice` arrays are replaced with `[]` while their `diceCount` is preserved. During `Reveal` and `Finished` phases, all players receive the full state.

The earlier client-side filtering approach (frontend filtering the `dice` array before rendering) is superseded by this decision. The frontend should not apply any client-side dice visibility filtering — the server sends each client only what that client is allowed to see.

See `docs/specs/public-private-game-state.md` for the full platform design. This decision supersedes the original client-side filtering approach recorded here. OQ-LD-01 is resolved — see below.

---

## Theme specification

### Scope
The pirate theme applies **only to the in-game room view** while a Liar's Dice game is active. The platform header, lobby, waiting screen, and all other pages use the default platform theme (Blade Runner / Skyline aesthetic).

### Implementation
The room wrapper component adds `data-game-theme="pirates"` when rendering a Liar's Dice game. The game module's CSS file declares token overrides scoped to `[data-game-theme="pirates"]`.

### Aesthetic direction
Not cheesy pirate clichés. Think:
- Maritime navigation charts — dark navy and slate backgrounds, cream/parchment grid lines
- Weathered brass — warm gold-amber for accents and interactive elements, not bright yellow
- Dark ocean — deep teal/near-black surface colours
- Typography: uppercase, slightly tracked sans-serif for labels; no comic or novelty fonts

### CSS token overrides (scoped to `[data-game-theme="pirates"]`)

```css
[data-game-theme="pirates"] {
  --color-background:    #0d1b2a;   /* deep navy */
  --color-surface:       #152840;   /* dark ocean */
  --color-surface-raised: #1e3a57;  /* slightly lifted */
  --color-surface-hover:  #243f5e;
  --color-primary:       #c8973a;   /* weathered brass */
  --color-on-primary:    #0d1b2a;
  --color-border:        #2a4a6b;   /* dark teal */
  --color-text:          #e8dfc8;   /* parchment */
  --color-text-muted:    #8aa3bb;
  /* Radius tightened slightly — charts feel more precise than rounded */
  --radius-sm:           2px;
  --radius-md:           4px;
}
```

These overrides use the same token names as the platform's `tokens.css` so the game can theme using standard variables without branching component logic.

---

## Frontend components

### `<DiceFace value={n} size="md" />` (SVG)

A single die face rendered as an inline SVG. Renders pip layouts for faces 1–6. Props:
- `value: 1 | 2 | 3 | 4 | 5 | 6`
- `size: 'sm' | 'md' | 'lg'` (maps to 32px / 48px / 64px)
- `highlighted?: boolean` — applies a brass-tinted border (used when a die matches the current bid face during Reveal)
- `wild?: boolean` — shown in the `Bidding` phase when `value === 1` and 1s are wild; adds a subtle wild indicator

### `<DiceCup player={...} isMe={bool} />` (container)

Renders a player's cup. When `isMe`:
- Shows all dice as `<DiceFace>` components
When not `isMe` and phase is `Bidding`:
- Shows `diceCount` face-down dice (a die outline without pips, aria-label "N hidden dice")
When phase is `Reveal` or `Finished`:
- Shows all dice for all players

### `<RevealAnimation />` (transition)

On `CallLiar`, before showing the reveal result, plays a CSS animation: cups tip over left-to-right, dice "roll out" over ~600ms. Implemented as a CSS keyframe animation triggered by a state flag (`phase === 'Reveal'`). Must respect `prefers-reduced-motion` — if set, shows results instantly without animation.

### `<BidControls />` (active player UI)

Only rendered for the current player when `phase === 'Bidding'`. Contains:
- A quantity stepper (down/up buttons, min 1, max = total active dice count across all players)
- A face selector (buttons 1–6, each showing a die face glyph)
- A "Place Bid" button (disabled if the current selection is not strictly higher than the current bid)
- A "Call Liar" button (disabled if `currentBid === null`, i.e. first bid of the round)
- A "Declare Palifico" button — only shown when the current player has exactly 1 die and `!hasUsedPalifico` and `currentBid === null`

### `<GameStatus />` (passive display)

Shows:
- Current bid (large type), or "No bid yet — make the opening bid"
- Palifico round banner when `palificoActive === true`
- `lastChallengeResult` string after a reveal resolves
- Whose turn it is (display name, highlighted if it is the local player)

---

## Backend implementation plan

### Module class: `LiarsDiceModule`

Extends `ReducerGameModule<LiarsDiceState, LiarsDiceAction, LiarsDiceOptions>`.

`CreateInitialState`: rolls dice for each player (using `System.Random`), sets `Phase = Bidding`, `CurrentPlayerIndex = 0`, `CurrentBid = null`, `RoundNumber = 1`.

`Validate`: returns a rejection reason string for any of:
- Wrong player acting
- Invalid phase for action type
- `PlaceBid` where the new bid is not strictly higher than `CurrentBid`
- `CallLiar` when `CurrentBid == null`
- `DeclarePalifico` when player has used it before, has more than 1 die, or bid already exists

`Apply`: pure state transition. For `CallLiar`:
1. Count matching dice (apply wild logic).
2. Determine loser.
3. Remove one die from loser's `Dice` list; update `DiceCount`.
4. If `DiceCount == 0`, set `Active = false`.
5. Check if only one active player remains — if so, set `Phase = Finished`, `Winner`.
6. Otherwise, set `Phase = Reveal`, populate `LastReveal`.
7. Return new state with `GameOverEffect` if finished.

For `StartNextRound`:
1. Re-roll all active players' dice (each player gets their full `DiceCount` new dice).
2. Clear `CurrentBid`, `LastReveal`, `LastChallengeResult`.
3. Advance `RoundNumber`.
4. Set `CurrentPlayerIndex` to the loser from the last round (or first active player if the loser was eliminated).
5. Reset `PalificoActive = false`.
6. Set `Phase = Bidding`.

### Options record

```csharp
public record LiarsDiceOptions(int StartingDice = 5);
// StartingDice: 3–7, default 5
```

`MinPlayers = 2`, `MaxPlayers = 6`, `AllowLateJoin = false`, `SupportsUndo = false`.

---

## Database

### Decision: no supplementary tables needed for v1

The primary game state lives in `rooms.game_state` (JSONB) per ADR-004. Liar's Dice does not require:
- Leaderboards (no persistent cross-room score)
- Match history accessible from outside the room
- Any data that outlives the room

The `LiarsDiceDbContext` will be scaffolded by `scripts/new-game.ps1` as required by the architecture, but its `OnModelCreating` will contain only the read-only platform views (Rooms, RoomPlayers, Users) and no game-owned tables. The migration will be an empty initial migration.

If per-player stats (win count, games played) are wanted in a future story, they can be added as a separate story with a new migration to `LiarsDiceDbContext` — zero impact on the platform.

---

## API changes

None. All game interaction flows through the existing `SendAction` / `StateUpdated` SignalR contract. The `StartGame` action is sent via `SendAction` exactly as any other game action; the platform does not need a new endpoint.

---

## CI changes

One migration step must be added to the GitHub Actions backend job, following the Skyline pattern:

```yaml
- name: Apply Liar's Dice migrations
  run: dotnet ef database update
       --project src/games/Meepliton.Games.LiarsDice
       --context LiarsDiceDbContext
```

This is the only required change outside the game module itself. It is explicitly allowed by ADR-006 ("Adding a new game requires adding one migration step to the CI pipeline").

---

## Out of scope

- Server-side per-player state projection — **implemented** via `LiarsDiceModule.ProjectForPlayer`; no longer out of scope. See `docs/specs/public-private-game-state.md`.
- Persistent leaderboard or cross-room statistics — future story
- Async / pass-and-play mode (`SupportsAsync = false`)
- Undo (`SupportsUndo = false` — bluffing games with revealed information cannot be meaningfully undone)
- Sound effects
- Spectator chat

---

## Acceptance criteria (testable)

- [ ] `LiarsDiceModule.Validate` returns an error when a non-current player submits `PlaceBid`
- [ ] `LiarsDiceModule.Validate` returns an error when `PlaceBid` quantity/face is not strictly higher than `CurrentBid`
- [ ] `LiarsDiceModule.Validate` returns an error when `CallLiar` is submitted with `CurrentBid == null`
- [ ] `LiarsDiceModule.Apply(CallLiar)` sets `Phase = Reveal` and populates `LastReveal` correctly when the bid is not met
- [ ] `LiarsDiceModule.Apply(CallLiar)` sets `Phase = Reveal` and populates `LastReveal` correctly when the bid is met
- [ ] Wild logic: 1s count toward any non-1 bid face in a normal round
- [ ] Wild logic: 1s do not count as wild when the current bid is for face 1
- [ ] Wild logic: 1s do not count as wild in a Palifico round for any face
- [ ] `Apply(CallLiar)` emits `GameOverEffect(winnerId)` when the loser's `DiceCount` drops to 0 and they were the last active player
- [ ] `Apply(StartNextRound)` re-rolls all active players' dice, resets `CurrentBid` to null, and increments `RoundNumber`
- [ ] `Apply(DeclarePalifico)` sets `PalificoActive = true` and `HasUsedPalifico = true` for that player
- [ ] Scrutor discovers `LiarsDiceModule` at startup without changes to `Program.cs`
- [ ] `LiarsDiceDbContext` runs its empty initial migration against a fresh database without error
- [ ] Frontend: `<DiceFace value={4} />` renders an SVG with 4 pips
- [ ] Frontend: in `Bidding` phase, other players' dice are rendered face-down (pips not visible in DOM)
- [ ] Frontend: `RevealAnimation` does not play when `prefers-reduced-motion` is set
- [ ] Frontend: `[data-game-theme="pirates"]` is present on the room wrapper when a Liar's Dice game is active
- [ ] Frontend: `BidControls` "Place Bid" button is disabled when the selected bid is not strictly higher than `currentBid`

---

## Open questions

- **OQ-LD-01** ~~(non-blocking): Warn users in the UI that dice are hidden client-side only? Recommendation: no warning in v1 for a friends-only app. Does not block implementation.~~ **Resolved — closed.** Server-side projection implemented via `LiarsDiceModule.ProjectForPlayer`. The platform now sends each player only the state their perspective allows. No client-side filtering warning is needed. See `docs/specs/public-private-game-state.md`.
- **OQ-LD-02** (non-blocking): Should `StartNextRound` auto-advance after a fixed delay (e.g. 5 seconds after reveal) or require an explicit player action? Recommendation: require explicit action (any active player taps "Next Round") to avoid races and give players time to absorb the reveal. Can be revisited.
- **OQ-LD-03** (non-blocking): Should the pirate theme override be applied by the platform room wrapper (platform reads `gameTheme` from the game module metadata) or by the game component itself (game sets the attribute in its root element)? Recommendation: game component sets it on its own root `<div>`. This keeps the platform unaware of per-game themes and is consistent with ADR-008 (games own their entire UI).
