# Spec: F'That

**Status:** Agreed
**Date:** 2026-03-19
**Authors:** analyst + architect

---

## Problem

Meepliton currently has two game modules: Skyline (Acquire-style tile placement, 2–6 players, ~90 minutes) and Liar's Dice (dice bluffing, 2–6 players, ~20 minutes). The library has no fast, card-driven game that plays equally well at 3, 5, or 7 players and finishes in 15–20 minutes. Friend groups often want a palette cleanser between longer sessions or a game that works when the full 6-person Skyline crowd cannot gather. F'That fills that gap.

The game also introduces a new archetype: no tiles, no dice — just a hand of collected cards and a personal resource (chips) that depletes over time. It also introduces the first module to require `HasStateProjection: true`, because chip counts are private information.

---

## Solution

F'That is a comedic re-skin of the card game "No Thanks." Every card represents something undesirable, and players are desperately trying to avoid being stuck with it. The game is turn-based and ends automatically when the deck runs out.

Chip counts are private: each player sees only their own exact count. Opponents see only an opaque indicator. The remaining deck card count is visible to all players at all times.

---

## Game rules

**Deck setup:** 33 cards numbered 3–35. Before play, 9 are removed at random and set aside face-down — no player ever sees which ones. The remaining 24 cards are shuffled. One card is drawn as the initial face-up card; the other 23 form the remaining deck.

**Chips:** Each player starts with a fixed chip supply (default 11, configurable via `startingChips`). Chips are a spending resource and are never replenished. The `startingChips` value is clamped to [7, 15] in `CreateInitialState`.

**Turn:** The active player must do exactly one of:

- **Take** — collect the face-up card and all chips resting on it into their personal collection. Chips collected this way return to the player's supply. The next card from the deck is revealed face-up with zero chips on it. The player who just took now faces the new card (turn stays with them).
- **Pass** — pay 1 chip from their supply onto the face-up card, then turn advances clockwise to the next player. A player with zero chips cannot pass; they must take.

**End condition:** The game ends automatically the moment the last card in the deck is taken.

**Scoring:**

```
cardScore = sum of the lowest card in each consecutive run
total = cardScore − chipsRemaining
```

Cards form a consecutive run when they are numerically adjacent (e.g. 7, 8, 9). Only the lowest value in each run counts. Example: holding {5, 6, 7, 15, 16} scores 5 + 15 = 20. Chips remaining reduce the score. Lowest total wins. Ties are possible; all tied players share the win.

The scoring function sorts cards internally before computing runs. Cards are stored in insertion order in `FThatPlayer.cards` (for UI display); scoring never mutates that list.

**Player count:** 3–7 players. Below 3 the pass-or-take tension collapses. Above 7 the 24-card deck gives too few cards per player.

---

## State model

### C# records (`FThatModels.cs`)

```
FThatState
  phase:              FThatPhase              // Playing | GameOver
  players:            List<FThatPlayer>
  currentPlayerIndex: int                     // index into players list
  deck:               List<int>               // remaining face-down cards (23 at start), ordered
  faceUpCard:         int                     // current card on offer (drawn first; the 24th of 24 playable cards)
  chipsOnCard:        int                     // chips resting on the face-up card
  scores:             List<FThatScore>?       // null during Playing; populated at GameOver
  winners:            List<string>?           // player IDs in seat order; null during Playing

FThatPlayer
  id:           string
  displayName:  string
  avatarUrl:    string?
  seatIndex:    int
  chips:        int                           // FULL server-side value; stripped in projected state for opponents
  cards:        List<int>                     // collected cards, insertion order (not sorted)

FThatScore
  playerId:   string
  cardScore:  int                             // sum of chain minimums
  chips:      int                             // chips remaining at game end
  total:      int                             // cardScore - chips; lowest wins

FThatAction
  type: FThatActionType                       // Take | Pass

FThatOptions
  startingChips: int                          // default 11; clamped to [7, 15] in CreateInitialState
```

**Enums (both must carry `[JsonConverter(typeof(JsonStringEnumConverter))]`):**

```
FThatPhase       { Playing, GameOver }
FThatActionType  { Take, Pass }
```

### Projected state

Because chip counts are private, the module implements `ProjectStateForPlayer`. The server broadcasts the projected form — `FThatView` — not the raw `FThatState`.

```
FThatView                                     // broadcast to each player
  phase:              FThatPhase
  players:            List<FThatPlayerView>
  currentPlayerIndex: int
  deckCount:          int                     // deck.Count — card ORDER is not exposed
  faceUpCard:         int
  chipsOnCard:        int
  scores:             List<FThatScore>?
  winners:            List<string>?

FThatPlayerView
  id:           string
  displayName:  string
  avatarUrl:    string?
  seatIndex:    int
  chips:        int                           // exact value for self; -1 for all opponents
  chipsHidden:  bool                          // false for self; true for all opponents
  cards:        List<int>                     // always visible (collected cards are public)
```

The projection rule: when generating `FThatView` for player P, each `FThatPlayerView` is built from the corresponding `FThatPlayer`. For P's own entry, `chips` = exact value and `chipsHidden` = false. For all other players, `chips` = -1 and `chipsHidden` = true.

The `deck` field is never included in `FThatView`. Only `deckCount` (= `deck.Count`) is broadcast.

`HasStateProjection` must be set to `true` on the module, and `ProjectStateForPlayer(state, playerId)` must be implemented to produce and serialize `FThatView`.

### TypeScript mirror (`types.ts`)

```typescript
export type FThatPhase = 'Playing' | 'GameOver'
export type FThatActionType = 'Take' | 'Pass'

// FThatView is what the frontend receives — never FThatState directly
export interface FThatView {
  phase:              FThatPhase
  players:            FThatPlayerView[]
  currentPlayerIndex: number
  deckCount:          number
  faceUpCard:         number
  chipsOnCard:        number
  scores:             FThatScore[] | null
  winners:            string[] | null
}

export interface FThatPlayerView {
  id:          string
  displayName: string
  avatarUrl:   string | null
  seatIndex:   number
  chips:       number      // exact for self; -1 for opponents
  chipsHidden: boolean     // false for self; true for opponents
  cards:       number[]
}

export interface FThatScore {
  playerId:  string
  cardScore: number
  chips:     number
  total:     number
}
```

The `Game.tsx` component receives `FThatView` as its `state` type parameter. The component uses `chipsHidden` to determine whether to display the numeric chip count or an opaque indicator for each player.

### Supplementary tables

None. F'That has no leaderboard or match-history tables in scope. No `FThatDbContext` is needed.

---

## Acceptance criteria

All criteria are testable at the unit-test level against `FThatModule.Validate()`, `FThatModule.Apply()`, and `FThatModule.ProjectStateForPlayer()` without a running server.

- [ ] **AC-1 — Setup:** Given a host starts an F'That game with N players (3 ≤ N ≤ 7), when `CreateInitialState` is called, then: `deck` contains exactly 23 unique integers drawn from [3..35]; `faceUpCard` is a valid integer from [3..35] not present in `deck`; `chipsOnCard` equals 0; each player's `chips` equals `startingChips` (clamped to [7, 15]; default 11); `currentPlayerIndex` is 0; `phase` is `Playing`.

- [ ] **AC-2 — Pass (chips available):** Given it is player A's turn and A has at least 1 chip, when A dispatches `{ type: "Pass" }`, then: A's `chips` decreases by 1; `chipsOnCard` increases by 1; `currentPlayerIndex` advances to the next player clockwise (wrapping); `faceUpCard` and `deck` are unchanged; `phase` remains `Playing`.

- [ ] **AC-3 — Pass (no chips):** Given it is player A's turn and A has 0 chips, when A dispatches `{ type: "Pass" }`, then the action is rejected with "You have no chips — you must take the card." and the state is unchanged.

- [ ] **AC-4 — Take (mid-game):** Given it is player A's turn and `deck` contains at least 1 card, when A dispatches `{ type: "Take" }`, then: `faceUpCard` is appended to A's `cards` list; `chipsOnCard` chips are added to A's `chips` supply; the first element of `deck` becomes the new `faceUpCard`; `chipsOnCard` resets to 0; `deck` loses its first element; `currentPlayerIndex` stays on player A's index; `phase` remains `Playing`.

- [ ] **AC-5 — Take (last card):** Given it is player A's turn and `deck` is empty (this is the last card), when A dispatches `{ type: "Take" }`, then: the `faceUpCard` is added to A's `cards`; chips on the card are added to A's `chips` supply; `phase` transitions to `GameOver`; `scores` is computed for all players; the player(s) with the lowest `total` score are listed in `winners` in ascending `seatIndex` order; a `GameOverEffect` is returned carrying the ID of the winner with the lowest `seatIndex` among tied winners.

- [ ] **AC-6 — Chain scoring:** Given a player holds `cards` = [7, 8, 9, 20] (insertion order) and `chips` = 3 at game end, when end-game scoring runs, then `cardScore` = 27 (7 + 20), `chips` = 3, and `total` = 24. Cards 8 and 9 do not contribute to `cardScore`. The scoring function sorts the card list internally before computing runs; the stored `cards` list is not mutated.

- [ ] **AC-7 — Wrong turn:** Given player B dispatches any action when `currentPlayerIndex` points to player A, then the action is rejected with "It is not your turn." and the state is unchanged.

- [ ] **AC-8 — Private chip projection:** Given player A requests their game view, when `ProjectStateForPlayer(state, playerA.id)` is called, then player A's own `FThatPlayerView` has `chips` = exact count and `chipsHidden` = false; every other player's `FThatPlayerView` has `chips` = -1 and `chipsHidden` = true.

- [ ] **AC-9 — Deck count visible:** Given a game is in progress, when any player's projected state is generated, then `FThatView.deckCount` equals the number of cards remaining in `deck`, and the `deck` contents (card values and order) are not present in the projected state.

---

## Architecture decisions

### AD-1: Implement `IGameModule + IGameHandler` directly (not `ReducerGameModule`)

F'That implements `IGameModule` and `IGameHandler` as a single class, following the Liar's Dice pattern. It does not extend `ReducerGameModule<,,>`.

Rationale: `ReducerGameModule.Handle()` returns `new GameResult(Serialize(newState))` with no effects. The method is not virtual and cannot be overridden to inject `GameOverEffect`. The only supported approach for effect emission is to implement `IGameHandler.Handle()` directly — which is exactly what Liar's Dice does. F'That will use the Liar's Dice module as its implementation template.

Note: Skyline extends `ReducerGameModule` but does NOT emit `GameOverEffect` (possible existing gap in that module, not in scope here).

### AD-2: `HasStateProjection: true` with `FThatView` projected type

Chip counts are private per official No Thanks rules. The module sets `HasStateProjection = true` and implements `ProjectStateForPlayer` to produce `FThatView`. The `FThatView` type is the C# record that gets serialized and broadcast; `FThatState` is the server-side canonical store only. The TypeScript `types.ts` does not expose `FThatState` — only `FThatView`.

### AD-3: `deck` holds 23 cards; `faceUpCard` is the 24th

`CreateInitialState` draws the first card from the shuffled 24-card set as `faceUpCard` and stores the remaining 23 as `deck`. This makes the Take action straightforward: shift `deck[0]` to `faceUpCard`, reduce `deck` by one. The `deck` field is never included in the projected state; only `deckCount` (= `deck.Count`) is broadcast.

### AD-4: `startingChips` clamped silently

`CreateInitialState` applies `Math.Clamp(options.StartingChips, 7, 15)` rather than rejecting the room creation with an error. Options values are trusted host input. The default is 11 (official No Thanks chip count).

### AD-5: Scoring sorts cards internally; `cards` stored in insertion order

`FThatPlayer.cards` is stored in insertion order so the UI can show the sequence in which cards were collected. The end-game scoring function sorts a copy of the list before computing runs. The stored list is never mutated by the scoring function.

### AD-6: Co-winner tie resolution

`winners` lists all tied players in ascending `seatIndex` order. `GameOverEffect.WinnerId` carries the lowest-`seatIndex` winner's ID (single string, not a list). This is deterministic and consistent across all games.

### AD-7: No platform contract changes

F'That requires zero changes to `IGameModule`, `IGameHandler`, `GameContext`, `GameResult`, `GameEffect`, or the TypeScript `GameModule`/`GameContext` interfaces. All existing contracts are sufficient.

---

## Out of scope

- Comedic card/chip naming and UI copy (deferred to `docs` agent)
- Leaderboard, statistics, or match history tables
- Undo support
- Spectator mode or late-join
- Game options beyond `startingChips`
- UI/UX design decisions beyond the functional `Game.tsx` skeleton (deferred to `/ui-design fthat`)
- Exposing which of the 9 removed cards were discarded (unknowable by design)
- Platform hook for `ReducerGameModule` to emit effects (log as a future platform improvement story)

---

## Implementation hints

These are guidance notes for the backend and frontend agents — not acceptance criteria.

**Backend:**

- Follow `LiarsDiceModule.cs` as the implementation template. The class implements both `IGameModule` and `IGameHandler`. `Handle()` delegates to private `Validate()` and `Apply()` methods, then checks the resulting phase to decide whether to wrap the result with `GameOverEffect`.
- `ProjectStateForPlayer(string state, string playerId)` deserializes `FThatState`, builds `FThatView` by mapping each player to `FThatPlayerView` (masking opponents), and returns the serialized `FThatView`.
- `CreateInitialState` generates [3..35], shuffles with `Random.Shared`, takes the first 9 as discards (do not store), takes the next card as `faceUpCard`, and assigns the remaining 23 as `deck`.
- Both `FThatPhase` and `FThatActionType` must carry `[JsonConverter(typeof(JsonStringEnumConverter))]`.
- No `FThatDbContext` — no EF migrations required.

**Frontend:**

- The `state` type parameter in `Game.tsx` is `FThatView`, not `FThatState`.
- Use `chipsHidden` to decide rendering: if `false`, display the numeric chip count; if `true`, display an opaque indicator (exact UI is `/ui-design` scope).
- Display `state.deckCount` prominently — all players should always know how many cards remain.
- The active player is `state.players[state.currentPlayerIndex]`. Compare `.id` to `myPlayerId` to determine whether to enable action buttons.
- Dispatch `{ type: "Take" }` or `{ type: "Pass" }` — both camelCase on the wire (matching the C# enum serialization via `JsonStringEnumConverter`).
