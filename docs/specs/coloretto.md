# Feature: Coloretto game module

**Story:** story-034
**Status:** Draft — ready for `/story-review`
**Date:** 2026-03-26

---

## Platform identity

**In-game name:** Chameleon Market
**Tagline:** "Collect wisely. Everything else costs you."

The name references the joker (chameleon) cards and the market-stall feel of claiming rows of goods. The theme is abstract and colourful — Mondrian meets board game.

---

## Summary

Coloretto is a push-your-luck set-collection game for 2–5 players. On your turn you either draw the top card from the deck and add it to any row, or take an entire row and collect its cards. Rows fill up to 3 cards — the more you add, the better the row becomes for whoever takes it, but also the riskier if you needed it yourself. After the game-end card appears (and the current round finishes), players score: their best 3 colour groups score positively on an escalating scale; all other colours score negatively on the same scale. Coloretto is chosen as a Meepliton game because the draw-or-take decision is simple to explain, deeply strategic, and maps cleanly to a satisfying browser UI.

---

## User stories

- As a player on my turn, I want to draw the top card and place it on a row so that I can set up a tempting row for others or benefit myself.
- As a player on my turn, I want to take a complete row and add its cards to my collection so that I can score points.
- As a player, I want to see all open rows and which cards are in them so that I can make informed decisions.
- As a player, I want to see my own collection grouped by colour so that I can track which colours are my top 3.
- As a player, I want to see an estimate of my current score so that I can understand the consequences of taking a row.
- As a player, I want to know when the end-game card has appeared so that I can plan my final moves.
- As the host, I want the game to end automatically after the round triggered by the end-game card.

---

## Game rules

### Deck composition
- 9 available colours; the active subset is fixed by player count (resolved — OQ-CO-03):

| Players | Active colours | Colour names |
|---|---|---|
| 2 | 3 | Brown, Blue, Green |
| 3 | 5 | Brown, Blue, Green, Orange, Purple |
| 4 | 6 | Brown, Blue, Green, Orange, Purple, Red |
| 5 | 7 | Brown, Blue, Green, Orange, Purple, Red, Yellow |

- Each active colour has 9 cards.
- 3 Joker (chameleon) cards — count as any colour at scoring time.
- 1 End-of-game trigger card ("Last Round" card) — shuffled into the lower half of the deck.
- **No "+2 point cards"** — these are not part of standard Coloretto and are not implemented in v1.

Total deck: varies by player count (active-colour cards + 3 jokers + 1 end card).

### Rows
- Row count = players + 1: 2p → 3 rows, 3p → 4 rows, 4p → 5 rows, 5p → 6 rows.
- Each row can hold a maximum of **3 cards**.
- **If all rows are full (3 cards each) and at least one player has not yet taken**, the only valid action for the current player is `TakeRow`. `DrawCard` is rejected.
- An empty row can be drawn into by any player.
- A row with 3 cards cannot receive more cards; it must be taken.

### Turn structure
On your turn you must do exactly one of:
1. **Draw** — Flip the top card from the deck face-up and place it on any row that is not full (has < 3 cards). Then your turn ends. If the drawn card is the End-of-game trigger, place it like a normal card; the current round continues to its end, then the game ends.
2. **Take** — Take all cards currently in any one row (even if it has 0 or 1 card). Add those cards to your personal collection. You may not take another row or draw this round — you sit out for the remainder of the round.

### Round end
A round ends when all players have taken a row. All taken rows are cleared. New empty rows begin for the next round. Players who haven't taken yet go in their original seating order in the new round.

### Game end trigger
When the End-of-game card is drawn it is **placed in a row like any other card** (resolved — OQ-CO-02), occupying a slot. It is immediately visible to all players as a special marker. After the current round finishes (everyone has taken a row), the game ends. The player who takes the row containing the end card simply receives the colour cards in that row; the end card itself has no point value and is discarded.

### Scoring
For each player:
1. Group collected cards by colour. Jokers are assigned to whichever colour gives the most benefit (handled server-side at scoring time — assign greedily).
2. Identify the player's **top 3 colour groups** (by card count; ties broken arbitrarily — player may choose).
3. Score the top 3 groups **positively** using the scale below.
4. Score all remaining groups **negatively** using the same scale.

**Scoring scale (per colour group):**

| Cards | Points |
|---|---|
| 1 | 1 |
| 2 | 3 |
| 3 | 6 |
| 4 | 10 |
| 5 | 15 |
| 6 | 21 |
| 7 | 28 |
| 8 | 36 |
| 9 | 45 |

(Scale continues for 8 and 9 cards — achievable in a 5-player game with all 9 cards of a colour.)

The player with the highest total score wins. Ties: joint win.

---

## State shape

### C# records

```csharp
public record ColorettoState(
    ColorettoPhase       Phase,
    List<ColorettoPlayer> Players,
    List<string>         Deck,           // remaining cards (not sent to clients)
    List<ColorettoRow>   Rows,
    int                  CurrentPlayerIndex,
    bool                 EndGameTriggered,  // true once the trigger card has been drawn
    RoundScoreResult?    FinalScores,       // only set in Finished phase
    string?              Winner             // userId (or null if tie)
);

public record ColorettoPlayer(
    string               Id,
    string               DisplayName,
    string?              AvatarUrl,
    int                  SeatIndex,
    Dictionary<string, int> Collection,  // colour → count (includes joker counts)
    bool                 HasTakenThisRound,
    bool                 Active          // always true in Coloretto (no elimination)
);

public record ColorettoRow(
    int          RowIndex,
    List<string> Cards    // 0–3 colour names or "Joker"
);

public record RoundScoreResult(
    List<PlayerScore> Scores
);

public record PlayerScore(
    string                 PlayerId,
    Dictionary<string, int> Collection,
    List<string>           TopColors,       // the 3 colours scored positively
    Dictionary<string, int> ColorScores,   // per-colour score (positive for top 3, negative for rest)
    int                    Total
);

public enum ColorettoPhase
{
    Waiting,
    Playing,
    Finished
}
```

### TypeScript mirror

```typescript
export type ColorettoPhase = 'Waiting' | 'Playing' | 'Finished'

export interface ColorettoState {
  phase:               ColorettoPhase
  players:             ColorettoPlayer[]
  deckSize:            number              // projected: count of remaining cards
  rows:                ColorettoRow[]
  currentPlayerIndex:  number
  endGameTriggered:    boolean
  finalScores:         RoundScoreResult | null
  winner:              string | null
}

export interface ColorettoPlayer {
  id:                 string
  displayName:        string
  avatarUrl:          string | null
  seatIndex:          number
  collection:         Record<string, number>
  hasTakenThisRound:  boolean
}

export interface ColorettoRow {
  rowIndex: number
  cards:    string[]
}

export interface RoundScoreResult {
  scores: PlayerScore[]
}

export interface PlayerScore {
  playerId:    string
  collection:  Record<string, number>
  topColors:   string[]
  colorScores: Record<string, number>
  total:       number
}
```

---

## Hidden information

The only hidden information is the **deck** (card order / remaining cards). Players do not need to know what cards remain (though they can infer from cards played). `Deck` is not sent to clients; `deckSize` (integer) is projected instead.

`HasStateProjection = true` (minimal — just strips the deck). Alternatively, `HasStateProjection = false` if the deck is stored separately or the full state sans-deck is always safe. Recommend `true` for consistency and to keep the deck hidden.

---

## Actions

### C# action type

```csharp
public record ColorettoAction(
    string  Type,
    int?    RowIndex  = null   // for DrawCard and TakeRow
);
```

| `Type` | Payload | Who | When |
|---|---|---|---|
| `StartGame` | — | Host | `Waiting` |
| `DrawCard` | `RowIndex` | Current player | `Playing`, hasn't taken this round |
| `TakeRow` | `RowIndex` | Current player | `Playing`, hasn't taken this round |

### TypeScript

```typescript
export type ColorettoAction =
  | { type: 'StartGame' }
  | { type: 'DrawCard'; rowIndex: number }
  | { type: 'TakeRow';  rowIndex: number }
```

---

## Apply logic

### DrawCard
1. Validate: player is current, hasn't taken this round, `rowIndex` row has < 3 cards.
2. Pop top card from `Deck`.
3. If card is `"EndGame"`: set `EndGameTriggered = true`; place the card in the row as a placeholder (or don't add it and just mark triggered — open question OQ-CO-02).
4. Otherwise: add card to `Rows[rowIndex].Cards`.
5. Advance `CurrentPlayerIndex` to next player who hasn't taken yet (skip players who already took).
6. Check for round end (all players have taken → clear rows, start new round).
7. If `EndGameTriggered` and round just ended → run scoring → `Phase = Finished`.

### TakeRow
1. Validate: player is current, hasn't taken, row exists (even if empty).
2. Move all cards from `Rows[rowIndex].Cards` into `player.Collection`.
3. Set `player.HasTakenThisRound = true`.
4. Advance `CurrentPlayerIndex` to next player who hasn't taken yet.
5. Check for round end (all players have taken):
   - Clear all rows (reset to empty).
   - Reset `HasTakenThisRound = false` for all players.
   - If `EndGameTriggered`: run scoring, `Phase = Finished`.
   - Otherwise: start next round with the same current player order (player order preserved; whoever's turn it "should" be next based on seat order starts).

### Round end / next player sequencing
After a player takes a row, they sit out for the rest of the round. The `CurrentPlayerIndex` cycles only through players who have NOT yet taken this round.

### Scoring (at game end)
For each player:
1. Assign jokers greedily: for each joker, add 1 to the colour group that currently benefits most from one additional card (highest marginal score gain).
2. Sort colour groups by size descending; pick top 3 as positive-scoring.
3. For each colour group: apply scale. Top 3 → positive. Rest → negative.
4. Sum all colour scores for total.
5. Sort players by total descending; winner = highest (ties = joint win, `Winner = null`).

---

## Frontend sketch

### `<RowDisplay rows currentPlayerId hasTakenThisRound />`
Shows all rows as horizontal card stacks. Each row shows its cards face-up (colour chips or card art). An empty row slot shows a "+" placeholder. Row is highlighted on hover if the current player can take or draw into it. Full rows (3 cards) show "TAKE ONLY" indicator.

### `<ActionButtons onDraw onTake selectedRow />`
Two buttons: "Draw here" (places drawn card on selected row) and "Take this row". Active only for the current player. Draw button disabled if selected row is full.

### `<PlayerCollection player isMe />`
Shows the player's collected cards grouped by colour, sorted by count. For own collection, shows estimated score ("Score if top 3 are: X, Y, Z = N pts"). For other players, shows colour counts only.

### `<ScoreSummary finalScores />`
End-game overlay with per-player breakdown: collection, chosen top 3 colours, per-colour points, total. Winner highlighted.

### `<EndGameBanner />`
Appears when `endGameTriggered = true`. "Final round in progress — game ends when everyone has taken a row."

### Visual / theme direction

Abstract, bold, and colourful — big coloured squares representing cards, inspired by Mondrian-style colour field paintings. The UI should feel almost toy-like in its clarity: strong colour blocks, thick outlines, minimal chrome.

`data-game-theme="chameleon-market"` on room wrapper.

```css
[data-game-theme="chameleon-market"] {
  --color-background:      #f5f5f0;   /* off-white canvas */
  --color-surface:         #ffffff;
  --color-surface-raised:  #f0f0ea;
  --color-surface-hover:   #e8e8e0;
  --color-primary:         #1a1a1a;   /* near-black for contrast on light bg */
  --color-on-primary:      #ffffff;
  --color-border:          #1a1a1a;   /* thick Mondrian-style borders */
  --color-text:            #1a1a1a;
  --color-text-muted:      #555550;
  --radius-sm:             0px;       /* hard edges — no rounding */
  --radius-md:             0px;
}
/* Card colour tokens (used for the colour-chip cards) */
[data-game-theme="chameleon-market"] {
  --card-brown:   #8B4513;
  --card-blue:    #1565C0;
  --card-green:   #2E7D32;
  --card-orange:  #E65100;
  --card-purple:  #6A1B9A;
  --card-red:     #C62828;
  --card-yellow:  #F9A825;
  --card-pink:    #AD1457;
  --card-white:   #EEEEEE;
}
```

---

## Joker assignment UI

At game end, jokers are assigned server-side greedily. This is correct for most cases but may not always be optimal (greedy ≠ globally optimal). For v1: server assigns greedily and result is shown in the score breakdown. A future story could allow players to manually assign jokers before scoring is finalised.

---

## Backend implementation plan

- `ColorettoModule : IGameModule, IGameHandler`
- `HasStateProjection = true`
- `MinPlayers = 2`, `MaxPlayers = 5`
- `AllowLateJoin = false`, `SupportsAsync = true` (turns are strictly sequential and short)
- `SupportsUndo = false`

### Key helpers
- `BuildDeck(playerCount)` → active colours, jokers, end card, shuffled
- `ScoreCollection(collection)` → per-player score breakdown using the 7-step scale
- `AssignJokersGreedily(collection)` → returns updated collection with jokers assigned

### Colour constants
```csharp
public static class ColorettoColors
{
    // 9 available; subset active based on player count
    public static readonly string[] All = ["Brown", "Blue", "Green", "Orange", "Purple", "Red", "Yellow", "Pink", "White"];
    public static int ActiveColorCount(int playerCount) => playerCount switch {
        2 => 3, 3 => 5, 4 => 6, 5 => 7, _ => 7
    };
}
```

---

## Database

No supplementary tables required for v1. `ColorettoDbContext` exists (scaffolding requirement) but owns no tables.

---

## CI changes

One migration step must be added to the GitHub Actions backend job:

```yaml
- name: Apply Coloretto migrations
  run: dotnet ef database update
       --project src/games/Meepliton.Games.Coloretto
       --context ColorettoDbContext
```

---

## Out of scope

- Player colour-group selection for top 3 (server picks optimally in v1)
- Manual joker assignment by player (greedy server assignment in v1)
- Coloured chameleon card art (colour chips/squares in v1)
- Async timeout (turns are quick enough that async play is fine; no timeout needed)

---

## Acceptance criteria

- [ ] `BuildDeck(playerCount)` produces correct number of active colours and includes 3 jokers and 1 end card
- [ ] `Validate(DrawCard)` rejects when selected row has 3 cards
- [ ] `Validate(TakeRow)` allowed even when row is empty (0 cards)
- [ ] `Validate(DrawCard/TakeRow)` rejects action from non-current player
- [ ] `Validate(DrawCard/TakeRow)` rejects from player who already took this round
- [ ] After `TakeRow`, cards transferred to player collection; row cleared
- [ ] Round ends when all players have taken; rows reset; `HasTakenThisRound` reset
- [ ] `EndGameTriggered` set when end-game card drawn; game ends after round completes
- [ ] Scoring scale correct: 1=1, 2=3, 3=6, 4=10, 5=15, 6=21, 7=28
- [ ] Top 3 colour groups score positively; all others score negatively
- [ ] Joker assigned to maximise positive score
- [ ] `FinalScores` populated with correct breakdown per player
- [ ] `ProjectStateForPlayer` hides deck contents; exposes `deckSize`
- [ ] `CurrentPlayerIndex` skips players who have already taken this round
- [ ] Frontend: full rows (3 cards) cannot receive `DrawCard` action
- [ ] Frontend: `EndGameBanner` appears when `endGameTriggered = true`
- [ ] Frontend: `PlayerCollection` shows colour groups with counts for all players
- [ ] Frontend: `ScoreSummary` shows final breakdown on game end

---

## Open questions

- **OQ-CO-01** (resolved): Tiebreak for top-3 colour selection — server picks the combination of 3 colours that maximises total score (exhaustive check; max 9 colours = 84 combinations at most). If still tied (same total score from different groupings), alphabetical colour order determines the top 3. Player override is a future story.
- **OQ-CO-02** (resolved): End-game card is placed in a row like a normal card. See "Game end trigger" section.
- **OQ-CO-03** (resolved): Canonical colour sets fixed per player count. See deck composition table above.
- **OQ-CO-04** (resolved): Row count = players + 1. Confirmed: 2p=3, 3p=4, 4p=5, 5p=6.
