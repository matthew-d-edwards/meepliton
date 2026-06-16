# Hex Escape — Rules

A cooperative puzzle for **1–6 players**. Work together to build a pipe route
that connects every survivor to the exit **before the zombie threat overruns
the building**. Everyone wins together, or everyone loses together — there is no
individual winner.

---

## 1. Goal

Each level places one or more **survivors** on a hex grid and one **exit**.
Players take turns placing and rotating **pipe tiles** to form a continuous open
path from every survivor to the exit.

- **You win** the moment every survivor is connected to the exit by an unbroken
  pipe route. The screen shows **"Escaped!"**
- **You lose** if the **threat counter** reaches the level's threshold first. The
  screen shows **"Overrun!"**

Because the game is cooperative, the result is shared by everyone in the room.

---

## 2. The board

The board is a grid of hexagonal cells addressed by **axial coordinates** `(q, r)`
and written as the string `"q,r"`. Each level defines exactly which cells exist,
so boards can be any shape — a corridor, an L-bend, a branching junction.

A cell is one of:

| Cell | Meaning |
|---|---|
| **Empty** | You may place a tile here. |
| **Pre-placed tile** | A fixed tile the level starts with. It **cannot be moved, rotated, or replaced**. |
| **Wall** | Impassable. No tile may ever be placed here. |
| **Survivor** | A survivor's starting cell. Survivors never move — the route comes to them. |
| **Exit** | The single win target. |

### The six directions

Every hex has **6 edges**, numbered `0`–`5`. Each direction points to the
neighbouring cell at a fixed coordinate offset:

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
every open edge:

```
open edges after rotation k  =  { (e + k) mod 6  |  e in base edges }
```

For example, a **Straight** tile (base `{0, 3}`) at rotation `1` opens edges
`{1, 4}` (NE–SW).

---

## 4. How pipes connect

Two neighbouring tiles are connected **only if both sides of the shared edge are
open**.

Concretely, a tile at cell **C** with an open edge in direction **d** connects to
its neighbour **N** if and only if:

1. **N exists** on the board, **and**
2. **N's tile has an open edge** in the opposite direction `(d + 3) mod 6`.

An open edge that points to a **wall, the board boundary, or an empty cell** is a
**dead end** — it simply leads nowhere. That is allowed; it is not an error.

The **exit** behaves like any other cell: a tile placed next to the exit connects
to it through a matching open edge.

A **survivor is connected** when there is an unbroken chain of connected open
edges from the exit to that survivor's cell.

---

## 5. Taking a turn

Players act in a shared room on **one shared board**. The turn model is
**free-order**: on each round, any player who has **not yet acted this round** may
take their turn. (You cannot act twice in the same round — if you try, the game
replies *"It is not your turn."*)

On your turn you take **exactly one** of these actions:

### Place a tile — `Place tile`
Choose an empty, non-wall cell and place a tile from the shared **hand**, picking
its rotation as you place it.

- The cell must be **on the board** — otherwise: *"Cell is not on the board."*
- The cell must be **empty** — otherwise: *"Cell is already occupied."*
- The hand must still contain that tile type — otherwise: *"No tiles of that type
  remaining."*
- Rotation must be `0`–`5` — otherwise: *"Invalid rotation."*

Placing a tile **uses one** of that type from the hand.

### Rotate a tile — `Rotate tile`
Re-orient a tile **you previously placed**.

- The cell must hold a player-placed tile — an empty cell gives *"No tile to
  rotate."*
- **Pre-placed (fixed) tiles cannot be rotated** — *"Cannot rotate a fixed tile."*
- Rotating to the **same** rotation is allowed, and still uses up your turn.

### Pass — `Pass`
Do nothing this turn. Useful when you have no helpful move, but be careful — the
threat still advances.

---

## 6. The zombie threat

Pressure comes from a single **threat counter**, not from monsters moving on the
board.

- After **every player has acted once** (a full **round**), the threat counter
  **goes up by 1**, and a new round begins.
- If the threat counter reaches **or exceeds** the level's **threat threshold**,
  the survivors are overrun and **everyone loses**.

This makes the clock completely predictable: you know exactly how many rounds you
have. In a **solo** game a round is a single turn, so the threat rises after each
of your actions.

> If everyone keeps passing, the threat will climb every round until it hits the
> threshold and the game is lost. That stalemate is intentional — there is no
> rule forcing progress, only the ticking threat.

---

## 7. Winning and losing

After each action the game first checks for a **win**, then advances the threat.

- **Win takes priority.** If your action completes the route for every survivor,
  you win immediately — the threat does **not** tick up that turn, even if your
  action also completed the round.
- If you did **not** win and your action completed the round, the threat rises;
  if it has reached the threshold, you lose.

Both outcomes end the game for the whole room with a shared result screen —
**"Escaped!"** or **"Overrun!"** — showing the final threat count and level name.

---

## 8. Levels

The host chooses a level from a dropdown when creating the room. v1 ships three
authored levels, each guaranteed solvable:

| Level | Name | Survivors | What you fill in | Threat threshold |
|---|---|:---:|---|:---:|
| `tutorial-01` | The Straight Path | 1 | 3 cells in a straight row | 5 |
| `medium-01` | The Bend | 1 | 3 cells around an L-corner | 4 |
| `hard-01` | Two Roads | 2 | 4 cells feeding a junction | 3 |

If no level is chosen (or an unknown one is requested), the game falls back to
**The Straight Path** (`tutorial-01`).

---

## 9. A quick worked example

**The Straight Path** (`tutorial-01`) is five cells in a row:

```
[S]──[ ]──[ ]──[ ]──[X]
(-2,0) (-1,0) (0,0) (1,0) (2,0)
```

- `(-2,0)` is the **survivor**, holding a fixed **Straight** tile open E–W.
- `(2,0)` is the **exit**, also a fixed **Straight** tile open E–W.
- The three middle cells are empty; the hand holds **3 Straight tiles**.

Place a **Straight** tile at rotation `0` (open E–W) in each of `(-1,0)`, `(0,0)`,
and `(1,0)`. Each tile's **W** edge meets its left neighbour's **E** edge, forming
an unbroken E–W line from the exit all the way to the survivor. The moment the
last gap is filled, the survivor is connected and you see **"Escaped!"** — with
threat to spare (the threshold is 5).

---

## 10. Designing a level

A level (`HexEscapeLevel`, static C# data in `HexEscapeLevels.cs`) specifies:

- `Cells` — every valid cell on the board.
- `Walls` — cells that exist but can never hold a tile.
- `PrePlacedTiles` — fixed starting tiles (coord, type, rotation).
- `SurvivorStartCells` — one or more survivor cells (a level **must** have at
  least one).
- `ExitCell` — the single exit.
- `TileHandCounts` — how many of each tile type the shared hand holds.
- `ThreatThreshold` — how many rounds before the survivors are overrun.

A level must be **solvable** and must **not** already be solved at setup (the
starting pre-placed tiles alone must not connect any survivor to the exit).

---

## 11. Accessibility

- Every board cell is keyboard-focusable and announces its coordinate, contents,
  and role (survivor, exit, wall, tile, empty).
- The tile picker is a focus-trapped dialog; **Escape** cancels it.
- The threat-danger and "all survivors connected" states use shape/text cues, not
  colour alone.
- The result is announced to screen readers as a live alert.

---

*Hex Escape is Meepliton's first cooperative game. See `docs/specs/hexescape.md`
for the full specification and acceptance criteria.*
