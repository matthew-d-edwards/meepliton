# Feature: F'That

**Story:** [story-030](../stories/story-030-fthat-game.md)
**Status:** Draft — Round 1 analyst spec (awaiting architect review)
**Date:** 2026-03-19

---

## Summary

F'That is a comedic re-skin of the card game "No Thanks" for the Meepliton platform. Players take turns deciding whether to take a face-up card (gaining it and any chips piled on it) or pay one chip to pass the card to the next player. Cards score against you, but consecutive runs are scored by their lowest value only — making chain-building the central strategic tension. The player with the lowest score wins.

The game is fast (15–30 minutes), works well at 3–7 players, and needs no hidden information — making it a clean fit for the `ReducerGameModule` pattern without state projection. It validates the platform's turn-based chip/card archetype distinct from the existing dice (Liar's Dice) and tile-placement (Skyline) games.

---

## User stories

- As a player, I want to join a room and play a quick card game with friends so that we have a lightweight option that doesn't require the full commitment of Skyline.
- As a player on my turn, I want to either take the face-up card or pay a chip to pass so that I can weigh short-term chip gain against long-term card score cost.
- As a player, I want consecutive card runs to score only their lowest value so that collecting a chain becomes a strategic goal rather than a liability.
- As a player, I want to see all players' card collections and chip counts at all times so that I can make informed decisions about passing.

---

## Game rules (source of truth for implementation)

### Deck setup

- Cards numbered 3 to 35 inclusive (33 cards total).
- Before play, exactly 9 cards are removed from the deck at random (face-down, unseen by anyone).
- The remaining 24 cards are shuffled. One card is flipped face-up to begin play.

### Chips

- Each player starts with a fixed number of chips determined by player count (see Open Question OQ-F1 below).
- The conventional "No Thanks" chip count is 11 chips per player regardless of player count.
- Chips are a spending resource; they are not replaced during the game.

### Turn structure

On each turn, the active player must choose exactly one of:

1. **Take** — collect the face-up card and all chips resting on it; those chips join the player's personal supply. The next card from the deck is flipped face-up (chips on it reset to zero). The player who took keeps the turn (they go again from the new card). Wait — standard No Thanks rules: the player who takes goes next (i.e. turn passes to that same player, who now faces the new card). See Open Question OQ-F2 for clarification.
2. **Pass** — pay 1 chip from personal supply onto the face-up card. Turn advances clockwise to the next player. If the player has 0 chips, Pass is forbidden; they must Take.

### End condition

The game ends immediately when the last card in the deck is taken. There is no "end game" declaration; it is automatic.

### Scoring

Each player computes:

```
score = (sum of lowest card in each consecutive run) - (remaining chips in hand)
```

**Consecutive run rule:** Cards in a player's collection that form an unbroken ascending sequence are treated as a single run. Only the minimum value in each run contributes to the score. Example: holding cards {5, 6, 7, 15, 16} yields runs [5,6,7] and [15,16], scoring 5 + 15 = 20, not 5+6+7+15+16=49.

**Lowest score wins.** Ties are possible; all tied players are co-winners.

### Theming

The game is called "F'That". Cards represent increasingly undesirable outcomes (the theme is that nobody wants the high-value cards — the name reflects the reaction to being stuck with them). The exact comedic copy for card names, chip labels, and UI text is out of scope for this spec — that is `docs` agent territory post-implementation.

---

## Acceptance criteria

- [ ] **Given** a host starts an F'That game with 3–7 players joined, **when** `CreateInitialState` is called, **then** the deck contains exactly 24 cards drawn from [3..35] after removing 9 at random, one card is face-up with 0 chips on it, each player has the configured starting chip count, and `currentPlayerIndex` points to player 0 (seat order).
- [ ] **Given** it is player A's turn and they have at least 1 chip, **when** they dispatch `{ type: "Pass" }`, **then** A's chip count decreases by 1, the chips-on-card count increases by 1, turn advances to the next active player clockwise, and state phase remains `Playing`.
- [ ] **Given** it is player A's turn and they have 0 chips, **when** they dispatch `{ type: "Pass" }`, **then** the action is rejected with "You have no chips — you must take the card." and state is unchanged.
- [ ] **Given** it is player A's turn, **when** they dispatch `{ type: "Take" }`, **then** the face-up card is added to A's card list, the chips on the card are added to A's chip supply, the next card from the deck becomes face-up with 0 chips, and the turn remains with player A (to face the new card).
- [ ] **Given** player A takes the last card in the deck, **when** `Apply` processes the action, **then** the phase transitions to `GameOver`, scores are computed for all players using chain-scoring, the player(s) with the lowest score are designated winners, and a `GameOverEffect` is returned carrying the lowest-scoring player's ID (first by seat index if tied).
- [ ] **Given** a player holds cards {7, 8, 9, 20}, **when** end-game scoring runs, **then** their card score contribution is 7 + 20 = 27 (not 7+8+9+20=44).
- [ ] **Given** any player dispatches any action when it is not their turn, **then** the action is rejected with "It is not your turn." and state is unchanged.

---

## API changes

No new REST endpoints or SignalR messages. The game uses the existing `SendAction` / `StateUpdated` / `GameOver` flow.

---

## Data model changes

### C# state records (`FThatModels.cs`)

```
FThatState
  phase:              FThatPhase         (Playing | GameOver)
  players:            List<FThatPlayer>
  currentPlayerIndex: int
  deck:               List<int>          // remaining face-down cards
  faceUpCard:         int                // current card on offer
  chipsOnCard:        int                // chips placed on the face-up card
  scores:             List<FThatScore>?  // null until GameOver

FThatPlayer
  id:           string
  displayName:  string
  avatarUrl:    string?
  seatIndex:    int
  chips:        int
  cards:        List<int>  // cards collected, unsorted

FThatScore
  playerId:  string
  cardScore: int
  chips:     int
  total:     int           // cardScore - chips; lowest wins

FThatAction
  type: FThatActionType   (Take | Pass)

FThatOptions
  startingChips: int      // default: 11
```

### TypeScript mirror (`types.ts`)

```typescript
FThatState {
  phase:              'Playing' | 'GameOver'
  players:            FThatPlayer[]
  currentPlayerIndex: number
  deck:               number[]
  faceUpCard:         number
  chipsOnCard:        number
  scores:             FThatScore[] | null
}

FThatPlayer {
  id:          string
  displayName: string
  avatarUrl:   string | null
  seatIndex:   number
  chips:       number
  cards:       number[]
}

FThatScore {
  playerId:  string
  cardScore: number
  chips:     number
  total:     number
}
```

### Enum decoration requirement

`FThatPhase` and `FThatActionType` must carry `[JsonConverter(typeof(JsonStringEnumConverter))]`.

---

## Module metadata

| Property | Value |
|---|---|
| `GameId` | `"fthat"` |
| `Name` | `"F'That"` |
| `MinPlayers` | 3 |
| `MaxPlayers` | 7 |
| `AllowLateJoin` | false |
| `SupportsAsync` | false |
| `SupportsUndo` | false |
| `HasStateProjection` | false (all information is public) |

---

## Implementation pattern

F'That is a clean fit for `ReducerGameModule<FThatState, FThatAction, FThatOptions>`. The state is a flat snapshot; there is no hidden information; actions are atomic single-step mutations. No `DbContext` is required — F'That has no supplementary tables (no leaderboard or match history in scope for this story).

---

## Out of scope

- Comedic card/chip naming and UI copy (deferred to `docs` agent after implementation)
- Leaderboard or per-player statistics tables
- Undo support (the game is fast enough that undo adds complexity without value)
- Spectator mode or late-join
- Game options beyond `startingChips` (the chip count is the only meaningful configurable parameter)
- Any UI/UX design beyond what `Game.tsx` needs to render the functional game (deferred to `/ui-design fthat`)

---

## Open questions

| # | Question | Impact | Blocks implementation? |
|---|---|---|---|
| OQ-F1 | What is the correct starting chip count? Standard "No Thanks" uses 11 chips per player regardless of player count. Some variants scale chips with player count to control game length. Recommend: default 11, configurable via `FThatOptions.startingChips`. | Game balance | No — default to 11; can be changed without contract change |
| OQ-F2 | After a player takes a card, does the turn stay with them (facing the new card) or pass clockwise? Standard "No Thanks" rules: the taker faces the new card — turn stays with them. This spec assumes that. Architect should confirm. | Turn logic correctness | Yes — must be resolved before `Apply` is implemented |
| OQ-F3 | Should the removed 9 cards be tracked in state (so players know the total card count removed but not which ones), or omitted entirely? Tracking the count is trivial; tracking which cards were removed reveals information. Recommend: store only the count, not the identities. | State shape | No — recommend omit identities; store `removedCount: 9` as a constant if desired |
| OQ-F4 | Player count bounds: "No Thanks" officially supports 3–7. Should Meepliton enforce the 7-player cap strictly, or allow up to 8 (the platform default upper bound)? With 8 players and 24 cards, some players may go most of the game without taking a card. Recommend 7 as the hard cap. | `MaxPlayers` value | No — default to 7; architect confirms |
| OQ-F5 | Does the `ReducerGameModule` base class's `Handle()` method currently support emitting a `GameOverEffect` without overriding `Handle()` directly? Liar's Dice overrides `Handle()` to inject the effect. Skyline uses `ReducerGameModule` but also overrides `Handle()`. The base class `Handle()` does not emit effects — F'That will need to override `Handle()` the same way as Liar's Dice if it is to emit `GameOverEffect`. Architect to confirm the recommended approach. | Implementation pattern | Yes — must be resolved before backend implementation |
