# Feature: Coup game module

**Story:** story-032
**Status:** Draft — ready for `/story-review`
**Date:** 2026-03-26

---

## Summary

Coup is a hidden-role bluffing game for 2–6 players. Each player controls two face-down influence cards (characters) and tries to eliminate all other players' influence. On your turn you take an action — which may require claiming a character you may or may not actually hold. Any player can challenge a claim, and targeted players can attempt to block. The last player with any influence wins. Coup is chosen as a Meepliton game because it is fast (15–30 min), deeply replayable, and introduces a new interaction pattern: multi-step turn resolution with asynchronous responses from multiple players.

---

## User stories

- As a player on my turn, I want to choose an action so that I can advance my strategy.
- As any player, I want to challenge another player's character claim so that I can call out bluffs.
- As a targeted player, I want to attempt to block an action directed at me so that I can protect myself.
- As a challenger of a block, I want to be able to call out the block too so that blocks cannot be used freely.
- As a player who loses a challenge, I want to choose which influence card to reveal so that I control what information is exposed.
- As a player who has lost both influence cards, I want to spectate the rest of the game so that I can follow the action.
- As the host, I want to start the game when all players are ready.

---

## Game rules

### Setup
- 2–6 players.
- Deck: 15 cards — 3 each of Duke, Assassin, Captain, Ambassador, Contessa.
- Each player receives 2 coins and draws 2 influence cards (face-down). Players see only their own cards.
- The player to the left of the host goes first; play is clockwise.

### Characters and their abilities

| Character | Action | Block |
|---|---|---|
| Duke | Tax: gain 3 coins | Block Foreign Aid |
| Assassin | Assassinate: pay 3 coins, eliminate 1 influence | — |
| Captain | Steal: take 2 coins from a player | Block Steal (by Captain or Ambassador) |
| Ambassador | Exchange: draw 2 cards, return 2 | Block Steal |
| Contessa | — | Block Assassination |

### Actions available each turn

| Action | Cost | Blockable by | Challengeable |
|---|---|---|---|
| Income | — | No | No |
| Foreign Aid | — | Duke | No |
| Coup | 7 coins | No | No |
| Tax (Duke) | — | — | Yes |
| Assassinate (Assassin) | 3 coins | Contessa | Yes |
| Steal (Captain) | — | Captain / Ambassador | Yes |
| Exchange (Ambassador) | — | — | Yes |

**Mandatory Coup:** if a player begins their turn with 10 or more coins, they **must** perform a Coup.

### Turn structure (multi-step)

A turn proceeds through these steps:

**Step 1 — Active player declares action.**
- Announcements: "I take Income", "I use the Duke to Tax", "I Assassinate [player]", etc.

**Step 2 — Response window.**
- For challengeable actions: all other players may challenge OR pass.
- For Foreign Aid: all other players may block (claim Duke) OR pass.
- For Steal / Assassinate: the targeted player may block OR all players may challenge.
- If no response within the window (all pass): proceed to resolution.

**Step 3 — Challenge resolution (if challenged).**
- If the claimant does NOT hold the character: they lose 1 influence (choose which card to reveal). Action fails.
- If the claimant DOES hold the character: they reveal it, shuffle it back into the deck, draw a replacement. The challenger loses 1 influence.

**Step 4 — Block (if a block is attempted, unchallenged).**
- Action fails. Turn ends.

**Step 5 — Block challenge (if a block is challenged).**
- Same logic as Step 3, but applied to the blocker's claim.
- If the blocker's claim is FALSE: blocker loses influence; original action resolves.
- If the blocker's claim is TRUE: challenger loses influence; action is still blocked.

**Step 6 — Action resolution.**
- Income: +1 coin.
- Foreign Aid: +2 coins.
- Coup: target loses 1 influence (they choose which).
- Tax: +3 coins.
- Assassinate: target loses 1 influence (they choose which, unless also blocked/challenged).
- Steal: take up to 2 coins from target (take all their coins if they have fewer than 2).
- Exchange: draw 2 cards from deck; choose 2 to keep (must keep exactly 2); return rest.

### Losing influence
Losing influence = revealing one face-down card of your choice. Once revealed it stays face-up and has no further effect.

### Elimination
A player with 0 face-down influence cards is eliminated and becomes a spectator.

### Win condition
Last player with at least 1 face-down influence card.

---

## State shape

### Turn phases (sub-states within a turn)

```
ActionDeclared → AwaitingResponses → (ChallengeResponse | BlockResponse | BlockChallengeResponse | InfluenceLoss) → Resolution → NextTurn
```

### C# records

```csharp
public record CoupState(
    CoupPhase           Phase,
    List<CoupPlayer>    Players,
    List<string>        Deck,              // remaining cards (not sent to clients)
    int                 ActivePlayerIndex,
    PendingAction?      Pending,           // null when phase is Waiting or Finished
    string?             Winner
);

public record CoupPlayer(
    string             Id,
    string             DisplayName,
    string?            AvatarUrl,
    int                SeatIndex,
    List<InfluenceCard> Influence,         // projected: other players see only face-up cards
    int                Coins,
    bool               Active             // false = eliminated
);

public record InfluenceCard(
    string  Character,   // "Duke" | "Assassin" | "Captain" | "Ambassador" | "Contessa"
    bool    Revealed     // true = face-up, eliminated card
);

public record PendingAction(
    string         ActionType,       // "Income" | "ForeignAid" | "Coup" | "Tax" | "Assassinate" | "Steal" | "Exchange"
    string         ActorId,
    string?        TargetId,         // null for non-targeted actions
    PendingStep    Step,
    List<string>   PassedPlayers,    // player IDs who have passed in AwaitingResponses
    string?        BlockerId,        // player currently attempting a block
    string?        ChallengerId,     // player who challenged
    List<string>?  ExchangeOptions,  // cards drawn for Ambassador exchange
    string?        InfluenceLossPlayerId  // who needs to choose an influence to lose
);

public enum CoupPhase
{
    Waiting,
    AwaitingResponses,   // waiting for other players to challenge/block/pass
    InfluenceLoss,       // a player must choose which card to reveal
    Exchange,            // Ambassador choosing which 2 cards to keep
    Finished
}

public enum PendingStep
{
    ActionResponses,     // waiting for challenges/blocks to the original action
    BlockResponses,      // waiting for a challenge to a block
    ResolvingChallenge,
    ResolvingBlock
}
```

### TypeScript mirror

```typescript
export type CoupPhase = 'Waiting' | 'AwaitingResponses' | 'InfluenceLoss' | 'Exchange' | 'Finished'

export interface CoupState {
  phase:               CoupPhase
  players:             CoupPlayer[]
  activePlayerIndex:   number
  pending:             PendingAction | null
  winner:              string | null
}

export interface CoupPlayer {
  id:          string
  displayName: string
  avatarUrl:   string | null
  seatIndex:   number
  influence:   InfluenceCard[]   // hidden cards projected as { character: null, revealed: false }
  coins:       number
  active:      boolean
}

export interface InfluenceCard {
  character: string | null   // null = hidden face-down card
  revealed:  boolean
}

export interface PendingAction {
  actionType:             string
  actorId:                string
  targetId:               string | null
  step:                   string
  passedPlayers:          string[]
  blockerId:              string | null
  challengerId:           string | null
  influenceLossPlayerId:  string | null
  exchangeOptions:        string[] | null   // null unless Ambassador exchange in progress
}
```

---

## Hidden information strategy

Each player's face-down influence cards must not be visible to opponents.

`ProjectStateForPlayer` replaces other players' hidden `InfluenceCard` entries with `{ character: null, revealed: false }`. Revealed cards (`revealed: true`) are always shown to everyone with their character name.

`HasStateProjection = true`.

Ambassador exchange: the `ExchangeOptions` list is only projected to the Ambassador player; all others see `null`.

---

## Actions

### C# action type

```csharp
public record CoupAction(
    string   Type,
    string?  TargetId         = null,   // target player ID
    string?  Character        = null,   // character being claimed for a block
    string?  InfluenceToLose  = null,   // card character to reveal when losing influence
    List<string>? KeepCards   = null    // Ambassador exchange: two cards to keep
);
```

| `Type` | Payload | Who | When |
|---|---|---|---|
| `StartGame` | — | Host | `Waiting` |
| `TakeIncome` | — | Active player | Their turn |
| `TakeForeignAid` | — | Active player | Their turn |
| `DoCoup` | `TargetId` | Active player | Their turn, 7+ coins |
| `TakeTax` | — | Active player (claims Duke) | Their turn |
| `Assassinate` | `TargetId` | Active player (claims Assassin) | Their turn, 3+ coins |
| `Steal` | `TargetId` | Active player (claims Captain) | Their turn |
| `Exchange` | — | Active player (claims Ambassador) | Their turn |
| `Challenge` | — | Any non-active player | `AwaitingResponses` (ActionResponses or BlockResponses step) |
| `Block` | `Character` | Target or any player (depending on action) | `AwaitingResponses` (ActionResponses step) |
| `Pass` | — | Any non-active non-passed player | `AwaitingResponses` |
| `LoseInfluence` | `InfluenceToLose` | The designated player | `InfluenceLoss` phase |
| `ChooseExchange` | `KeepCards` (2 items) | Active Ambassador player | `Exchange` phase |

### TypeScript

```typescript
export type CoupAction =
  | { type: 'StartGame' }
  | { type: 'TakeIncome' }
  | { type: 'TakeForeignAid' }
  | { type: 'DoCoup';          targetId: string }
  | { type: 'TakeTax' }
  | { type: 'Assassinate';     targetId: string }
  | { type: 'Steal';           targetId: string }
  | { type: 'Exchange' }
  | { type: 'Challenge' }
  | { type: 'Block';           character: string }
  | { type: 'Pass' }
  | { type: 'LoseInfluence';   influenceToLose: string }
  | { type: 'ChooseExchange';  keepCards: string[] }
```

---

## Apply logic (key transitions)

### Action declared
State transitions to `AwaitingResponses` with `PendingAction` set. Income and Coup skip the response window and resolve immediately (not challengeable/blockable).

### All players pass (ActionResponses step)
Resolve action immediately.

### Challenge submitted (ActionResponses step)
Transition to `InfluenceLoss`:
- If actor does NOT have the claimed card: actor must lose influence → action fails, then back to next turn.
- If actor DOES have the card: reveal it, shuffle back, draw replacement → challenger must lose influence → action resolves.

### Block submitted (ActionResponses step)
Transition to `AwaitingResponses` with `Step = BlockResponses`. Blocker claim is stored.

### All players pass (BlockResponses step)
Block succeeds → action fails → next turn.

### Challenge submitted (BlockResponses step)
- If blocker does NOT have the claimed card: blocker loses influence → action resolves normally.
- If blocker DOES have the card: challenger loses influence → block stands → action fails.

### LoseInfluence
Player reveals the specified card. If that player now has 0 unrevealed cards: `Active = false`. Check for game over (1 active player remaining).

---

## Frontend sketch

### `<InfluenceCards player isMe />`
Shows 2 card slots. Face-down cards: card back image. Revealed cards: character art + strikethrough. For `isMe`, face-down cards show the character name/art.

### `<ActionPanel />`
Active player sees action buttons (disabled based on coin count and game state). Mandatory Coup warning when ≥ 10 coins.

### `<ResponsePanel pending isMe />`
Non-active players see: "Challenge", "Block" (if eligible), "Pass". Clarifies what character block requires.

### `<ChallengeLog />`
Scrollable feed of resolved actions, challenges, and blocks with outcomes.

### `<ExchangeModal options />`
Ambassador sees 4 cards (their 2 + 2 drawn); must pick exactly 2 to keep.

### Visual / theme direction
Political intrigue aesthetic: dark charcoal backgrounds, muted gold accents, character portraits in a playing-card style. `data-game-theme="coup"` on room wrapper.

---

## Backend implementation plan

- `CoupModule : IGameModule, IGameHandler`
- `HasStateProjection = true`
- `MinPlayers = 2`, `MaxPlayers = 6`
- `AllowLateJoin = false`, `SupportsAsync = false`, `SupportsUndo = false`

Multi-step resolution is managed entirely in server state — no separate SignalR "sub-channel" needed. Each response (`Challenge`, `Block`, `Pass`, `LoseInfluence`, `ChooseExchange`) is a normal `SendAction` call.

---

## Database

No supplementary tables required for v1. `CoupDbContext` exists (scaffolding requirement) but owns no tables.

---

## Out of scope

- Reformation expansion (allegiances, converting) — future story
- Async / pass-and-play mode — blocked by multi-step real-time response requirement
- Auto-timeout for responses — future story
- Sound/animation on challenge resolution

---

## Acceptance criteria

- [ ] `Validate` rejects actions from non-active players (except `Challenge`, `Block`, `Pass`, `LoseInfluence`, `ChooseExchange` during their respective phases)
- [ ] `Validate` rejects `DoCoup` when player has < 7 coins
- [ ] `Validate` forces `DoCoup` as the only valid action when player has ≥ 10 coins
- [ ] `Validate` rejects `Assassinate` when player has < 3 coins
- [ ] Challenge correctly resolves: wrong claim → actor loses influence + action fails
- [ ] Challenge correctly resolves: true claim → actor draws replacement + challenger loses influence + action resolves
- [ ] Block correctly blocks Foreign Aid, Assassination, and Steal when unchallenged
- [ ] `LoseInfluence` eliminates player when they have no unrevealed cards remaining
- [ ] `GameOverEffect` emitted when only 1 active player remains
- [ ] `ProjectStateForPlayer` hides face-down influence cards of other players
- [ ] `ProjectStateForPlayer` hides `ExchangeOptions` from non-Ambassador players
- [ ] Steal takes min(2, target.coins) coins
- [ ] Exchange: actor draws 2 from deck, returns 2, keeps 2; deck updated correctly
- [ ] Frontend: `ActionPanel` shows only valid actions given current coin count
- [ ] Frontend: `ResponsePanel` visible to all non-active players during `AwaitingResponses`
- [ ] Frontend: eliminated players can observe but have no interactive controls

---

## Open questions

- **OQ-CU-01** (blocking): What is the response timeout? If a player is slow to respond, the game stalls. Recommendation: display a "Pass" auto-timer (30–60 seconds); auto-pass on expiry. Requires platform timer support or a game-owned timer tick.
- **OQ-CU-02** (non-blocking): When multiple players could block or challenge simultaneously, should the first response win, or should there be a brief window? Recommendation: first response wins for challenges; only the target can block in the first window (others can challenge the block).
- **OQ-CU-03** (non-blocking): Should eliminated players be able to chat? Recommendation: yes, eliminated players should stay in the room as spectators.
