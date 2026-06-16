# Hex Escape (Outbreak) — Rules

**v2 — Outbreak.** This document describes the current game. v1 (threat counter) is superseded
and preserved only in git history.

A cooperative game for **1–6 players**. Work together to navigate your characters
across a hex grid of pipe tiles and reach the exit before the zombie outbreak
eliminates everyone. Everyone wins together, or everyone loses together — there is
no individual winner.

---

## 1. Goal

Each level places zombie tokens on a hex grid, hides an exit tile somewhere in a
shared draw deck, and reserves one side of the board for characters to start from
and the opposite side for the exit to appear.

- **You win** when every surviving character physically occupies the exit cell at
  the same time. The screen shows **"Escaped!"**
- **You lose** when all characters who were ever placed on the board have been
  eliminated by zombies. The screen shows **"Overrun!"**

---

## 2. The board

The board is a grid of hexagonal cells addressed by **axial coordinates** `(q, r)`
and written as the string `"q,r"`. A level defines exactly which cells exist, so
boards can be any shape.

### Three zones

| Zone | What it is |
|---|---|
| **Spawn zone** | Where characters begin. Each player gets a reserved cell here. Normal tiles may be placed in the spawn zone. |
| **Exit zone** | Where the exit tile will appear. No player may place tiles here. |
| **Horde origin** | Cells (pre-tiled by the level) where new zombies emerge at each round boundary. |

### The six directions

Every hex has **6 edges**, numbered `0`–`5`. Each direction points to the
neighbouring cell:

| Direction | Offset (Δq, Δr) | Compass (flat-top) |
|:---:|:---:|:---:|
| 0 | (+1,  0) | E  |
| 1 | (+1, −1) | NE |
| 2 | ( 0, −1) | N  |
| 3 | (−1,  0) | W  |
| 4 | (−1, +1) | SW |
| 5 | ( 0, +1) | S  |

The edge **opposite** direction `d` is `(d + 3) mod 6`. (E↔W, NE↔SW, N↔S.)

---

## 3. Pipe tiles

A tile opens a passage on some of its 6 edges. There are **five tile types**.
The table shows each tile's **open edges before rotation** (rotation `0`):

| Tile | Open edges (rotation 0) | Shape |
|---|---|---|
| **Straight** | {0, 3} | A line through two opposite edges (E–W). |
| **Elbow** | {0, 1} | A bend joining two adjacent edges (E–NE). |
| **Tee** | {0, 1, 2} | A T joining three consecutive edges. |
| **Cross** | {0, 1, 2, 3} | A junction across four consecutive edges. |
| **Dead end** | {0} | A single open edge (a stub). |

### Rotation

A tile can be rotated in **60° steps**. Rotation `k` (a value `0`–`5`) shifts
every open edge by `k` positions:

```
open edges after rotation k  =  { (e + k) mod 6  |  e in base edges }
```

For example, a **Straight** tile (base `{0, 3}`) at rotation `1` opens edges
`{1, 4}` (NE–SW).

---

## 4. How pipes connect

Two neighbouring tiles are connected **only if both sides of the shared edge are
open**.

A tile at cell **C** with an open edge in direction **d** connects to its
neighbour **N** if and only if:

1. **N exists** on the board, **and**
2. **N's tile has an open edge** in the opposite direction `(d + 3) mod 6`.

An open edge that points to a wall, the board boundary, or an empty cell is a
**dead end** — it simply leads nowhere. That is allowed; it is not an error.

The same rule governs both character movement and zombie movement.

---

## 5. The deck and drawing tiles

Players draw tiles from a **shared deck**. The deck is shuffled at the start of
the game with several important properties:

- **Safe opening:** the top portion of the deck is guaranteed to contain no zombie
  tiles or the exit tile. Each player also receives a small starting hand dealt
  from this safe zone before the game begins.
- **Middle band:** zombie tiles are distributed here, mixed with normal tiles.
- **Exit band:** the single exit tile is placed at a random position in the last
  portion of the deck, so the exit always appears late but not at the very end.

Each player has their own **hand** (maximum 3 tiles). Draw a tile (costs 1 AP)
to add it to your hand; then place it from your hand on the board (costs 1 AP).

### Zombie tiles

If you draw a **zombie tile**, you must place it on the board before you can take
any other action this turn. Choose any occupied, non-exit-zone cell that does not
already have a zombie — a new zombie token spawns there immediately. If no valid
cell exists, the zombie tile is discarded instead.

### The exit tile

The exit tile is not placed by any player. When a player draws it, the server
places it automatically on the exit-zone cell closest to the board's centre.
The exit tile is a fixed **Cross** tile (all four sides open) — it cannot be
rotated. Once placed, `exitRevealed` becomes true and the exit cell is visible
to all players.

---

## 6. Taking a turn

### Seat claiming

Turns are not pre-ordered. When no one is currently taking a turn, any player
who has not yet acted this round may **claim the active turn** simply by taking
their first action. Once a turn is claimed, only that player can act until their
turn ends.

### Action points

At the start of your turn you receive an **action-point (AP) pool** (the size
depends on player count — fewer players get more AP per turn to compensate for
fewer people). Each action costs exactly 1 AP. Unused AP are lost when your turn
ends.

### The five actions

| Action | Cost | Qualifying? |
|---|:---:|:---:|
| **Draw tile** — draw the top card of the deck into your hand | 1 AP | Yes |
| **Place tile** — place a tile from your hand on an empty non-exit-zone cell | 1 AP | Yes |
| **Place zombie tile** — resolve your zombie-tile obligation (see §5) | 1 AP | Yes |
| **Move character** — move your character one hex along an open pipe connection | 1 AP | Yes |
| **Rotate tile** — re-orient any player-placed non-fixed tile on the board | 1 AP | **No** |

### The minimum-actions rule

Before you may end your turn voluntarily, you must have taken at least **2
qualifying actions** (the four actions marked "Yes" above). Rotating tiles does
not count toward this minimum.

If fewer than 2 qualifying actions are actually available to you — for example,
your hand is full, the deck is empty, and there is nowhere left to move or place —
then whatever actions are available to you is the new minimum.

**Rotate tile never blocks you from ending your turn**, even if all your AP went
on rotations and you took zero qualifying actions (provided no qualifying actions
were actually available).

### Forced zombie tile

If you are holding a zombie tile, you **must** resolve it before doing anything
else — including ending your turn. This takes priority over the minimum-actions
rule.

Special case: if you draw a zombie tile as your very last AP, the server resolves
the obligation automatically and your turn ends immediately. You are never left
holding a zombie tile with no AP to spend.

### Ending your turn

Your turn ends in one of two ways:

- You run out of AP (automatic — the system ends your turn for you).
- You choose to **End Turn** after meeting the minimum-actions requirement and
  clearing any zombie-tile obligation.

After your turn ends, the next player may claim the active turn.

---

## 7. Characters and movement

When you place your **very first tile** of the game, your character appears on
that tile. Your first tile must go in your **reserved spawn cell** — the cell
assigned to you in the spawn zone at the start of the game. Subsequent tiles can
go anywhere valid.

Once your character is placed you can move it one hex per **Move character**
action. Movement requires an open pipe connection in both directions: your tile
must have an open edge pointing toward the destination, and the destination tile
must have an open edge pointing back.

A character is **eliminated** the moment a zombie occupies the same cell — whether
from a zombie move, a zombie spawn, or you walking into a zombie. Eliminated
characters no longer count toward the win condition and cannot move, but their
player keeps taking turns and can still help by placing and rotating tiles.

---

## 8. Zombies

### How they start

Each level places one or more zombie tokens on the board at the start of the game,
on cells that already have pre-placed tiles.

### How they move (each round boundary)

After every player has taken their turn, a round boundary runs through four phases:

**Phase 1 — Transition:** the board enters zombie-movement mode (the phase tag in
the UI changes to "Zombie Move").

**Phase 2 — Containment break-out:** each zombie that has no valid move (every
direction is either off-board, leads to an empty cell, or the connection rule
fails in both directions) fights its way out:

1. The zombie's own tile rotates so it has an open edge toward the lowest-numbered
   direction that has an adjacent tiled cell. (Pre-placed level tiles are immune
   to this rotation — only player-placed tiles can be rotated by a breakout.)
2. A new zombie spawns on the adjacent in-grid tiled non-exit-zone cell in the
   lowest-numbered valid direction.

All containment decisions use a **frozen snapshot** of the board taken at the
start of Phase 2. A breakout from one zombie does not change the containment
verdict for later zombies in the same phase.

**Phase 3 — Zombie rolls:** every zombie (including any spawned in Phase 2) rolls
a d6. The die face maps to a direction: direction = die face mod 6. If the
connection rule is satisfied between the zombie's current cell and its neighbour
in that direction, the zombie moves there. Otherwise it stays put. Each roll and
result (moved / blocked) is shown in the zombie-movement overlay.

**Phase 4 — Horde spawn:** one or more new zombies spawn from the horde-origin
cells defined by the level. The number depends on player count — more players
face a faster-growing horde.

After all four phases, the loss condition is checked. If any placed characters
remain un-eliminated, the next round begins.

### Zombie co-location

Any character sharing a cell with a zombie is **eliminated immediately** — whether
the character moved into the zombie's cell, the zombie moved into the character's
cell, or a zombie spawned on the character's cell. The exit cell is never a valid
zombie spawn target, so reaching the exit is always safe from spawning.

Two or more zombie tokens may occupy the same cell at the same time.

---

## 9. Winning and losing

**Win:** after any action, if the exit has been revealed and every placed,
non-eliminated character is on the exit cell, the game ends immediately with
**"Escaped!"** The win check runs after each action — you can win in the middle
of your turn. Zombie movement does not run.

**Lose:** after zombie movement completes at the end of a round, if at least one
character was ever placed and all placed characters are now eliminated, the game
ends with **"Overrun!"**

**Unplaced players** (who have not yet placed their first tile) are ignored for
both win and loss checks — they are not in play.

---

## 10. Levels

The host chooses a level when creating the room. v2 ships one level:

| ID | Name | Board | Description |
|---|---|---|---|
| `tutorial-01` | The Outbreak | 9 × 5 hex grid (45 cells) | One starting zombie at the centre. Spawn zone on the left, exit zone on the right. Three horde-origin cells anchor the zombie supply. |

If no level is chosen, **The Outbreak** (`tutorial-01`) is used.

---

## 11. A quick worked example

**The Outbreak** (`tutorial-01`), 2-player game:

- Alice (seat 0) is assigned spawn cell `(-4, 0)`.
- Bob (seat 1) is assigned spawn cell `(-4, -1)`.
- Both start with 2 tiles in hand, dealt from the safe zone.
- One zombie starts at `(0, 0)`, which has a pre-placed Cross tile.

**Round 1, Alice's turn:**

Alice claims the active turn by taking her first action. She places a Straight
tile at rotation `0` (open E–W) in her reserved spawn cell `(-4, 0)`. Her
character appears there. She then draws a tile. That is 2 qualifying actions —
she meets the minimum and clicks "End Turn."

**Round 1, Bob's turn:**

Bob places a Tee tile in `(-4, -1)` (his spawn cell). His character appears.
He then rotates a tile already on the board. Rotating does not count as a
qualifying action, so he still has only 1. He draws another tile — now 2
qualifying actions — and ends his turn.

**Round boundary:**

Both seats have acted. Zombie movement begins. The zombie at `(0, 0)` rolls a 4
— direction 4 is SW `(-1, +1)`. The Cross tile is open in direction 4 and the
neighbouring cell `(-1, 1)` has a pre-placed Cross tile with direction 1 (NE,
the opposite) open. The zombie moves to `(-1, 1)`. Round 2 starts.

**Later — exit revealed:**

Alice draws the exit tile. The server places a fixed Cross tile in the exit-zone
cell closest to the centre. Both players now need to reach that cell with their
characters to win.

---

## 12. Accessibility

- Every board cell is keyboard-focusable and announces its coordinate, contents,
  and role (spawn cell, exit zone, zombie, character, tile, empty).
- The tile-picker and action-picker dialogs are focus-trapped; pressing Escape
  cancels them.
- The zombie-movement overlay is focus-trapped; pressing Escape or the Continue
  button dismisses it.
- Character elimination and exit-revealed events are announced to screen readers
  as assertive alerts.
- The turn indicator and zombie-move overlay use text and shape cues, not colour
  alone.
- The final result ("Escaped!" / "Overrun!") is announced as a live alert.

---

*Hex Escape is Meepliton's first co-op game. See `docs/specs/hexescape.md` for
the full specification and acceptance criteria (AC-v2-1 through AC-v2-79).*
