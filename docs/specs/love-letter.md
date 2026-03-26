# Feature: Love Letter game module

**Story:** story-033
**Status:** Draft — ready for `/story-review`
**Date:** 2026-03-26

---

## Summary

Love Letter is a micro card game for 2–4 players (standard deck). Each player holds exactly one card at a time; on their turn they draw a second card and must play one of the two, applying its effect. Players are eliminated by effects or by holding the lowest card when the deck runs out. Rounds are fast — often 2–5 minutes — and play continues until one player accumulates enough Tokens of Affection to win. Love Letter is chosen as a Meepliton game because it has an exceptionally small state surface, plays well asynchronously (turns are seconds-long and purely sequential), and rewards psychological reading of opponents.

---

## User stories

- As a player on my turn, I want to draw a card and choose which of my two cards to play so that I can make a strategic decision.
- As a player using the Guard, I want to name a character and target a player so that I can try to eliminate them.
- As a player using the Baron, I want to compare hands privately and have the lower card eliminated automatically so that the result is revealed without ambiguity.
- As a player behind Handmaid protection, I want to be immune to effects until my next turn so that my card is safe.
- As an eliminated player, I want to see the reason I was eliminated so that the game feels fair and legible.
- As a player, I want to see how many tokens each player holds so that I know who is closest to winning.
- As the host, I want to start a new round automatically after the previous one ends so that play stays fast.

---

## Game rules

### Setup
- 2–4 players.
- 16-card deck:

| Card | Value | Count | Effect |
|---|---|---|---|
| Guard | 1 | 5 | Name a character (not Guard) and target a player. If they hold that card, they are eliminated. |
| Priest | 2 | 2 | Look at another player's hand card (private, only you see it). |
| Baron | 3 | 2 | Compare hands with another player. The player with the lower value card is eliminated. Ties: nothing happens. |
| Handmaid | 4 | 2 | Until your next turn, you cannot be targeted by other players' effects. |
| Prince | 5 | 2 | Choose any player (including yourself). They discard their hand card (without effect) and draw a new one. If the deck is empty, they take the set-aside card. If they discard the Princess, they are eliminated. |
| King | 6 | 1 | Trade hands with another player. |
| Countess | 7 | 1 | If you hold the Countess together with the King or Prince, you **must** play the Countess. |
| Princess | 8 | 1 | If you discard or play the Princess for any reason, you are immediately eliminated. |

Total: 16 cards.

### Round setup
1. Shuffle the deck.
2. Set aside 1 card face-down (unknown for the round).
3. In a **2-player game**, additionally set aside 3 more cards face-up (these are known to all players but not in play).
4. Deal 1 card face-down to each player.

### Turn structure
1. The current player draws 1 card from the deck (now holds 2).
2. They play 1 card face-up to their discard pile, applying its effect immediately.
3. Play passes to the left.

### Countess rule (forced discard)
If a player's two cards are Countess + King **or** Countess + Prince, they **must** play the Countess on their turn. This is enforced by the server during validation.

### Handmaid immunity
A player who played Handmaid on their last turn is immune to being targeted. If all other players are protected by Handmaid, a targeting effect has no effect (the action still counts as played).

### Round end
A round ends when **either**:
- The deck is empty after the current player draws. All remaining players compare hand values; the highest card wins. Ties: tied players compare their discard pile total; highest total wins. Ties remain a tie and all tied players win the round.
- Only 1 player remains (all others eliminated).

### Tokens of Affection (win condition)
The round winner(s) each receive 1 Token of Affection. First player to reach the target token count wins the game:
- 2 players: 7 tokens
- 3 players: 5 tokens
- 4 players: 4 tokens

Ties for game win: simultaneous — both players win (very rare; announce as joint victory).

---

## State shape

### C# records

```csharp
public record LoveLetterState(
    LoveLetterPhase       Phase,
    List<LoveLetterPlayer> Players,
    List<string>          Deck,            // remaining draw pile (not sent to clients)
    string?               SetAsideCard,   // face-down removed card (not sent to clients)
    List<string>          FaceUpSetAside, // 2-player only: 3 known-removed cards (public)
    int                   CurrentPlayerIndex,
    int                   Round,
    RoundResult?          LastRoundResult,
    PriestReveal?         PendingPriestReveal,  // set when Priest played; cleared after viewer acks
    string?               Winner                // userId; set in GameOver phase
);

public record LoveLetterPlayer(
    string        Id,
    string        DisplayName,
    string?       AvatarUrl,
    int           SeatIndex,
    string?       HandCard,        // the card currently in hand (hidden from others)
    List<string>  DiscardPile,     // all cards played this round (face-up, public)
    int           Tokens,          // tokens of affection accumulated
    bool          Active,          // false = eliminated this round
    bool          Handmaid,        // true = currently protected by Handmaid
    bool          InGame           // false = eliminated from the full game (0 tokens path not applicable — players can't be knocked out of the game permanently in standard rules; keep for expansion compatibility)
);

public record RoundResult(
    List<string>  WinnerIds,
    string        Reason,          // "LastStanding" | "HighestCard" | "HighestDiscard"
    List<PlayerHandReveal> Reveals // hand values shown at round end
);

public record PlayerHandReveal(string PlayerId, string Card);

public record PriestReveal(
    string ViewerId,    // player who played Priest
    string TargetId,    // player whose card is revealed
    string Card         // the revealed card (only sent to viewer)
);

public enum LoveLetterPhase
{
    Waiting,
    Playing,
    RoundEnd,   // brief display of results before next round starts
    GameOver
}
```

### TypeScript mirror

```typescript
export type LoveLetterPhase = 'Waiting' | 'Playing' | 'RoundEnd' | 'GameOver'

export interface LoveLetterState {
  phase:               LoveLetterPhase
  players:             LoveLetterPlayer[]
  deckSize:            number              // projected: how many cards remain
  faceUpSetAside:      string[]            // 2-player: the 3 known-removed cards
  currentPlayerIndex:  number
  round:               number
  lastRoundResult:     RoundResult | null
  priestReveal:        PriestReveal | null // only set for the viewing player
  winner:              string | null
}

export interface LoveLetterPlayer {
  id:          string
  displayName: string
  avatarUrl:   string | null
  seatIndex:   number
  handCard:    string | null   // null for other players (hidden), own card visible to self
  discardPile: string[]
  tokens:      number
  active:      boolean
  handmaid:    boolean
}

export interface RoundResult {
  winnerIds: string[]
  reason:    string
  reveals:   PlayerHandReveal[]
}

export interface PlayerHandReveal {
  playerId: string
  card:     string
}

export interface PriestReveal {
  viewerId: string
  targetId: string
  card:     string
}
```

---

## Hidden information strategy

- Each player's `HandCard` is projected as `null` for all other players.
- `Deck` and `SetAsideCard` are not sent to clients; only `deckSize` is projected.
- `PriestReveal` is only included in the state projected to `viewerId`; all other players receive `priestReveal: null`.

`HasStateProjection = true`.

---

## Actions

### C# action type

```csharp
public record LoveLetterAction(
    string   Type,
    string?  CardPlayed   = null,  // the card being played
    string?  TargetId     = null,  // target player (Guard, Baron, Priest, Prince, King)
    string?  GuessedCard  = null   // Guard only: the character guessed
);
```

| `Type` | Payload | Who | When |
|---|---|---|---|
| `StartGame` | — | Host | `Waiting` |
| `PlayCard` | `CardPlayed`, `TargetId?`, `GuessedCard?` | Current player | `Playing` |
| `AcknowledgePriest` | — | Priest viewer | After Priest reveal |
| `StartNextRound` | — | Host | `RoundEnd` |

### TypeScript

```typescript
export type LoveLetterAction =
  | { type: 'StartGame' }
  | { type: 'PlayCard'; cardPlayed: string; targetId?: string; guessedCard?: string }
  | { type: 'AcknowledgePriest' }
  | { type: 'StartNextRound' }
```

---

## Apply logic

### PlayCard — routing by card type

**Guard:**
1. Validate: target is active, not Handmaid-protected, not the actor. Guessed card is not "Guard".
2. If target's hand card matches `GuessedCard`: target is eliminated.
3. Otherwise: nothing (card still played).

**Priest:**
1. Validate: target is active, not Handmaid-protected, not the actor.
2. Set `PendingPriestReveal = { viewerId: actorId, targetId, card: target.HandCard }`.
3. State transitions to wait for `AcknowledgePriest` (no phase change; actor is "frozen" until acknowledged).

**Baron:**
1. Validate: target is active, not Handmaid-protected, not the actor.
2. Compare actor's remaining hand card vs target's hand card.
3. Lower value: eliminated. Tie: nothing.

**Handmaid:**
1. No target.
2. Set actor's `Handmaid = true`.
3. On next turn start, clear `Handmaid` for the drawing player.

**Prince:**
1. Validate: target is active and (not Handmaid-protected OR target is the actor).
2. Discard target's hand card (no effect from discard — except Princess eliminates).
3. If Princess discarded: target eliminated.
4. Otherwise: target draws top card from deck (or SetAsideCard if deck empty).

**King:**
1. Validate: target is active, not Handmaid-protected, not the actor.
2. Swap actor's remaining hand card with target's hand card.

**Countess:**
1. No target.
2. No effect beyond being played.
3. Server validates Countess was played when holding King or Prince (forced discard rule).

**Princess:**
1. Actor is immediately eliminated (they played/discarded the Princess).

### After each PlayCard
- Move played card to actor's `DiscardPile`.
- Clear actor's Handmaid flag if it was set on a previous turn.
- Check elimination conditions.
- Advance `CurrentPlayerIndex` to next active player.
- Check round-end conditions: if deck is empty after draw or only 1 active player remains → `Phase = RoundEnd`.

### Round end
1. Collect all active players' hand cards for reveal.
2. Determine winner(s) by highest card; tie-break by highest discard total.
3. Award tokens to winner(s).
4. Check if any player has reached the win threshold → `Phase = GameOver`, set `Winner`.
5. Otherwise: `Phase = RoundEnd`, await `StartNextRound`.

### StartNextRound
Re-shuffle all 16 cards (ignoring what was played), re-run round setup, deal 1 card to each player, `Phase = Playing`.

---

## Countess validation

In `Validate(PlayCard)`:
- If `actor.HandCard` (the card NOT being played) is King or Prince, AND the card being played is NOT Countess, AND the actor holds Countess: return error "You must play the Countess when holding the King or Prince."
- Server tracks both cards the actor holds during validation (the played card + the one staying in hand).

---

## Frontend sketch

### `<PlayerHand card isMe />`
Single card slot. Own card shows face-up with art. Other players show a face-down card back. Card displayed large and centrally for the current player.

### `<ActionDrawer />`
When it is your turn: draw button appears (or auto-draw is triggered). After drawing: show both cards side by side; tap to select which to play. Contextual inputs appear for Guard (guess dropdown) and targeting (player selector).

### `<DiscardPile player />`
Row of face-up played cards in order. Used as context for reading the game.

### `<TokenTrack player count target />`
Tokens displayed as hearts (or similar). Shows progress toward the win threshold.

### `<RoundEndOverlay result />`
Shows hand reveal, winner announcement, token update, then "Start Next Round" button (host only).

### `<PriestRevealModal card targetDisplayName />`
Private modal shown only to the Priest viewer. "You saw [Name]'s card: [Card]". Dismissed with `AcknowledgePriest`.

### Visual / theme direction
Elegant royal-court aesthetic: deep burgundy, cream parchment, gold foil card borders, calligraphic card labels. `data-game-theme="love-letter"` on room wrapper.

---

## Backend implementation plan

- `LoveLetterModule : IGameModule, IGameHandler`
- `HasStateProjection = true`
- `MinPlayers = 2`, `MaxPlayers = 4`
- `AllowLateJoin = false`, `SupportsAsync = true` (sequential turns; fast enough to work async)
- `SupportsUndo = false` (hidden information; Priest reveal cannot be un-seen)

---

## Database

No supplementary tables required for v1. `LoveLetterDbContext` exists (scaffolding requirement) but owns no tables. Tokens are per-game session state only.

---

## Out of scope

- Love Letter Premium (6-player, extra characters like Chancellor) — future story
- Auto-advance round (host must click "Start Next Round" for v1)
- Animations for card plays and eliminations (v1 uses instant state update)

---

## Acceptance criteria

- [ ] `CreateInitialState` deals 1 card to each player; sets aside 1 face-down; for 2-player, also reveals 3 face-up
- [ ] `Validate(PlayCard)` enforces Countess rule: error if holding Countess + King/Prince and not playing Countess
- [ ] `Validate(PlayCard)` rejects targeting a Handmaid-protected player
- [ ] `Validate(PlayCard)` rejects Guard guessing "Guard"
- [ ] Guard: correct guess eliminates target; wrong guess has no effect
- [ ] Baron: lower-value hand is eliminated; tie has no effect
- [ ] Prince: target discards hand, draws new card; Princess discard → elimination
- [ ] Princess: actor eliminated immediately on play
- [ ] King: hands swapped between actor and target
- [ ] Priest: `PriestReveal` projected only to the Priest player; cleared after `AcknowledgePriest`
- [ ] Round ends when deck is empty after a draw or only 1 active player remains
- [ ] Tie-breaking: highest discard total wins; all-tie → all tied players win token
- [ ] Tokens awarded correctly; game ends when threshold reached
- [ ] Token thresholds: 2p=7, 3p=5, 4p=4
- [ ] `ProjectStateForPlayer` hides opponents' hand cards; hides SetAsideCard; projects deckSize correctly
- [ ] `StartNextRound` correctly reshuffles and re-deals
- [ ] Frontend: PriestRevealModal appears only for the viewing player
- [ ] Frontend: current player sees their own hand card face-up at all times

---

## Open questions

- **OQ-LL-01** (non-blocking): Should `StartNextRound` be host-only or any player? Recommendation: host-only for consistency, but this could be revisited.
- **OQ-LL-02** (non-blocking): Should the Priest acknowledgement be required before the turn advances, or can we just surface the information and auto-advance? Recommendation: require acknowledgement so the viewer has time to read the revealed card on mobile.
- **OQ-LL-03** (non-blocking): In the 2-player face-up set-aside cards, should these be displayed prominently (useful info) or subtly? Recommendation: show them as a "removed cards" list in a sidebar.
