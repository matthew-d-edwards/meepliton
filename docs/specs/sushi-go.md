# Feature: Sushi Go game module

**Story:** story-031
**Status:** Draft — ready for `/story-review`
**Date:** 2026-03-26

---

## Summary

Sushi Go! is a card-drafting game for 2–5 players. Each round, players are dealt a hand of cards and simultaneously pick one to keep, then pass their hand to the next player. This continues until all cards are played. After three rounds scores are totalled, with an end-of-game pudding bonus/penalty applied. Sushi Go is chosen as the next Meepliton game because it introduces simultaneous selection (a new interaction pattern for the platform), has rich visual card art potential, and plays well at all supported player counts.

---

## User stories

- As a player, I want to see my hand and pick exactly one card each turn so that I can draft strategically.
- As a player, I want to see which cards I have in front of me (my tableau) so that I can track my scoring progress.
- As a player, I want to see how many cards remain in other players' hands so that I can infer what they might be holding.
- As a player, I want all players' picks to be revealed simultaneously so that the drafting feels fair and tense.
- As a player, I want to see my current score and breakdown after each round so that I can adjust strategy.
- As a player, I want to see the final scores and pudding results at the end of the game so that I know who won.
- As a player holding Chopsticks, I want to swap them back and take two cards in a single turn so that I get the benefit of having saved Chopsticks.
- As the host, I want to start the game when all players are ready.

---

## Game rules

### Setup
- 2–5 players.
- Deck composition (108 cards total):

| Card | Count | Scoring |
|---|---|---|
| Tempura | 14 | Pairs = 5 pts |
| Sashimi | 14 | Sets of 3 = 10 pts |
| Dumpling | 14 | 1/2/3/4/5+ = 1/3/6/10/15 pts |
| Maki Roll ×3 | 8 | See Maki scoring |
| Maki Roll ×2 | 12 | See Maki scoring |
| Maki Roll ×1 | 6 | See Maki scoring |
| Salmon Nigiri | 10 | 2 pts (6 on wasabi) |
| Squid Nigiri | 5 | 3 pts (9 on wasabi) |
| Egg Nigiri | 5 | 1 pt (3 on wasabi) |
| Pudding | 10 | End-of-game only |
| Wasabi | 6 | ×3 multiplier for next nigiri |
| Chopsticks | 4 | Action card — see below |

- Cards dealt per round by player count: 2p=10, 3p=9, 4p=8, 5p=7.
- Three rounds are played; cards are shuffled between rounds. Pudding cards stay in players' tableaux across all rounds.

### Turn structure (simultaneous)
Each turn:
1. Each player secretly selects one card from their hand (or uses Chopsticks — see below).
2. All selections are revealed simultaneously.
3. Selected cards move to each player's tableau.
4. Hands are passed to the left.
5. Repeat until hands are empty.

### Chopsticks
- When Chopsticks are in your tableau, you may use them on your turn to pick **two** cards from your hand instead of one.
- To use: select both cards and "swap" Chopsticks back into your hand (Chopsticks return to the passing cycle).
- You cannot use Chopsticks when your hand has only 1 card.

### Round scoring
After all cards are played, score each player's tableau (except Pudding):

- **Tempura:** every complete pair scores 5 pts (e.g. 3 tempura = 5 pts; leftover single = 0).
- **Sashimi:** every complete set of 3 scores 10 pts (e.g. 5 sashimi = 10 pts).
- **Dumpling:** 1=1, 2=3, 3=6, 4=10, 5+=15 pts (total, not per card).
- **Maki Rolls:** sum each player's maki icon count. Player with most icons: 6 pts. Player with second-most: 3 pts. Ties split evenly (round down). In a 2-player game, only 1st place scores.
- **Nigiri:** each nigiri scores its base value (Salmon=2, Squid=3, Egg=1). If there is an unused Wasabi in the tableau beneath the nigiri, it scores 3× instead.
- **Wasabi without a nigiri:** 0 pts (wasted).
- **Chopsticks:** 0 pts.
- **Pudding:** not scored each round — kept for end-of-game.

### End-of-game pudding scoring (after round 3)
- Player with the **most** pudding cards: +6 pts.
- Player with the **fewest** pudding cards: −6 pts.
- Ties for most share the +6 (split evenly, round down). Ties for least share the −6.
- **Exception:** in a 2-player game, the −6 penalty is not applied.

### Game end
After round 3 scores and pudding scores are applied, the player with the most points wins. Ties are broken by the player with the most pudding cards (if still tied, it is a draw).

---

## State shape

### C# records

```csharp
public record SushiGoState(
    SushiGoPhase         Phase,
    List<SushiGoPlayer>  Players,
    int                  Round,            // 1–3
    int                  Turn,             // 1–handSize (within current round)
    List<string>         Deck,             // remaining undealt cards (card type names)
    List<List<string>>   Hands,            // Hands[seatIndex] = cards in that player's hand
    List<string>?        PendingPicks,     // null until all players have picked; Picks[seatIndex]
    string?              Winner            // userId; set in Finished phase
);

public record SushiGoPlayer(
    string        Id,
    string        DisplayName,
    string?       AvatarUrl,
    int           SeatIndex,
    List<string>  Tableau,          // cards scored this round + pudding across rounds
    List<int>     RoundScores,      // scores per completed round (length 0–3)
    int           PuddingCount,     // tracked separately for end-of-game scoring
    bool          HasPicked,        // has this player submitted their pick this turn?
    bool          UsingChopsticks   // is this player using their chopsticks this turn?
);

public enum SushiGoPhase
{
    Waiting,    // pre-game
    Picking,    // players selecting cards
    Revealing,  // all picked; short display pause before hands pass (auto-advance)
    Scoring,    // end of round — show scores, wait for host to advance
    Finished    // game over
}

// Card types as string constants (e.g. "Tempura", "Sashimi", "Dumpling",
// "Maki1", "Maki2", "Maki3", "SalmonNigiri", "SquidNigiri", "EggNigiri",
// "Pudding", "Wasabi", "Chopsticks")
```

### TypeScript mirror

```typescript
export type SushiGoPhase = 'Waiting' | 'Picking' | 'Revealing' | 'Scoring' | 'Finished'

export interface SushiGoState {
  phase:        SushiGoPhase
  players:      SushiGoPlayer[]
  round:        number
  turn:         number
  hands:        string[][]       // hands[seatIndex]; empty for other players (projected)
  winner:       string | null
}

export interface SushiGoPlayer {
  id:            string
  displayName:   string
  avatarUrl:     string | null
  seatIndex:     number
  tableau:       string[]
  roundScores:   number[]
  puddingCount:  number
  hasPicked:     boolean
  handSize:      number          // projected: how many cards in this player's hand
}
```

---

## Hidden information strategy

Each player's hand is private. The server projects state via `ProjectStateForPlayer`:
- The requesting player receives their full hand.
- All other players' `hands` entries are replaced with `[]`; a separate `handSize` integer is exposed so players can count remaining cards.
- The `Deck` is not sent to the client (it has no strategic information during play).

`HasStateProjection = true`.

---

## Actions

### C#

```csharp
public record SushiGoAction(
    string       Type,
    string?      Pick        = null,   // card type chosen (PickCard, UseChopsticks)
    string?      Pick2       = null,   // second card when using Chopsticks
    bool?        UseChopsticks = null
);
```

| `Type` | Payload | Who | When |
|---|---|---|---|
| `StartGame` | — | Host | `Waiting` phase |
| `PickCard` | `Pick: cardType` | Any player who hasn't picked | `Picking` phase |
| `UseChopsticks` | `Pick: cardType, Pick2: cardType` | Player with chopsticks in tableau, hasn't picked | `Picking` phase |
| `AdvanceRound` | — | Host | `Scoring` phase |

### TypeScript

```typescript
export type SushiGoAction =
  | { type: 'StartGame' }
  | { type: 'PickCard';       pick: string }
  | { type: 'UseChopsticks';  pick: string; pick2: string }
  | { type: 'AdvanceRound' }
```

---

## Apply logic

### PickCard / UseChopsticks
1. Validate: player hasn't already picked this turn; card is in their hand.
2. If UseChopsticks: validate player has Chopsticks in tableau; hand has ≥ 2 cards.
3. Record the pick (stored in `PendingPicks`); set `HasPicked = true`.
4. If all players have now picked → auto-advance to `Revealing` phase:
   a. Move all picked cards to player tableaux.
   b. If UseChopsticks: return Chopsticks to the picking player's hand before passing.
   c. Pass all hands left (wrap around).
   d. If all hands are empty → score the round → advance to `Scoring` (or `Finished` after round 3).
   e. Otherwise → advance to `Picking` phase for next turn.

### Scoring (server-side, triggered automatically)
Run `ScoreRound(state)`:
1. Maki roll totals per player → award 6/3 pts.
2. For each player: sum Tempura pairs, Sashimi trios, Dumplings, Nigiri (check Wasabi stacking), Chopsticks = 0.
3. Store in `RoundScores[round - 1]`.
4. Clear tableau of all cards except Pudding (Pudding persists).

If round 3 just finished:
5. Run `ScorePudding(state)`.
6. Set `Phase = Finished`, `Winner`.

### Wasabi stacking logic
In each player's tableau, scan left-to-right. Track `wasabiActive` flag. When a Wasabi is encountered: set `wasabiActive = true`. When a Nigiri is encountered and `wasabiActive`: apply ×3, set `wasabiActive = false`.

---

## Frontend sketch

### `<Hand cards={string[]} onPick={fn} onChopsticks={fn} />`
Fan of card images/icons. Tapping a card selects it (highlighted). A second tap (or separate "Play" button) confirms. When Chopsticks are in tableau, a "Use Chopsticks" mode allows selecting two cards.

### `<Tableau playerId cards={string[]} isMe={bool} />`
Shows that player's face-up played cards, arranged by type. Shows current round score in corner.

### `<ScoreBoard players roundScores puddingCounts />`
End-of-round overlay with per-player breakdown: card group scores, maki ranking, round total, cumulative total. Pudding column shows count; final game screen adds the +6/−6 row.

### `<WaitingIndicator players hasPickedMap />`
Shows which players have picked (checkmark) vs. still choosing (spinner). Does not reveal what they picked.

### Visual / theme direction
Cute Japanese restaurant aesthetic: warm paper-lantern pinks and reds, clean white card faces, bold card-type iconography. `data-game-theme="sushi-go"` on the room wrapper.

---

## Backend implementation plan

- `SushiGoModule : IGameModule, IGameHandler`
- `HasStateProjection = true`
- `MinPlayers = 2`, `MaxPlayers = 5`
- `AllowLateJoin = false`, `SupportsAsync = true` (turns are simultaneous, not sequential), `SupportsUndo = false`

### Key helpers
- `BuildDeck()` → returns shuffled 108-card `List<string>`
- `DealHands(deck, playerCount, round)` → returns hands per player
- `ScoreRound(state)` → pure scoring function; returns per-player round scores
- `ScorePudding(state)` → awards end-game pudding bonuses

---

## Database

No supplementary tables required for v1. State lives in `rooms.game_state` JSONB. `SushiGoDbContext` will exist (required by scaffolding) but own no tables.

---

## API changes

None — all interaction via `SendAction` / `StateUpdated` SignalR.

---

## Out of scope

- Sushi Go Party! variant cards (Onigiri, Uramaki, etc.) — future story
- Async mode (simultaneous play requires all players online; `SupportsAsync = false` until platform has a timeout/auto-pick fallback)
- Undo (simultaneous reveal makes undo non-trivial)

---

## Acceptance criteria

- [ ] `SushiGoModule.CreateInitialState` deals correct hand sizes per player count (2p=10, 3p=9, 4p=8, 5p=7)
- [ ] `Validate(PickCard)` rejects picks for a card not in the player's hand
- [ ] `Validate(PickCard)` rejects a second pick from a player who already picked this turn
- [ ] `Validate(UseChopsticks)` rejects when no Chopsticks in player's tableau
- [ ] After all players pick, hands pass to the left
- [ ] After all hands are empty, round scoring runs automatically
- [ ] Maki scoring: 6 pts for most maki icons; 3 pts for second (ties split)
- [ ] Maki scoring: in 2-player, only 1st place scores
- [ ] Wasabi doubles/triples the first nigiri played after it (Egg×3=3, Salmon×3=6, Squid×3=9)
- [ ] Dumpling scoring: 1/3/6/10/15 for 1/2/3/4/5+ dumplings
- [ ] Pudding persists across rounds; end-of-game +6/−6 applied correctly
- [ ] Pudding penalty (−6) not applied in 2-player game
- [ ] `ProjectStateForPlayer` hides other players' hands; exposes `handSize`
- [ ] After round 3, `Phase = Finished` and `Winner` is set
- [ ] Frontend: unselected cards in hand are not highlighted; selected card highlighted before confirm
- [ ] Frontend: `WaitingIndicator` shows who has/hasn't picked without revealing picks
- [ ] Frontend: `[data-game-theme="sushi-go"]` present on room wrapper during game

---

## Open questions

- **OQ-SG-01** (non-blocking): Should `AdvanceRound` be automatic (server auto-advances after a fixed delay) or host-initiated? Recommendation: host-initiated, matching the Liar's Dice `StartNextRound` pattern.
- **OQ-SG-02** (non-blocking): Should Chopsticks use a "mode toggle" UI (tap Chopsticks in tableau to enter multi-select mode) or a dedicated button? Recommendation: dedicated "Use Chopsticks" button visible only when applicable.
- **OQ-SG-03** (blocking): Does the platform support simultaneous action submission? All players submit independently — the server collects and auto-advances when all have submitted. Confirm `SendAction` under concurrent load does not double-advance.
