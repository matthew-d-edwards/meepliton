// Mirror of HexEscapeModels.cs (v2) — keep in sync with C# records.
// C# enums carry [JsonConverter(typeof(JsonStringEnumConverter))] → PascalCase strings on wire.
// C# record properties → camelCase JSON via JsonNamingPolicy.CamelCase.

export type Phase = 'Actions' | 'ZombieMovement' | 'GameOver'
export type Outcome = 'Escaped' | 'Overrun'
export type HexTileType = 'Straight' | 'Elbow' | 'Tee' | 'Cross' | 'Deadend'

// ── Grid ──────────────────────────────────────────────────────────────────────

export interface HexCell {
  tileType: HexTileType
  rotation: number
  fixed: boolean
}

// ── Deck / hand entries ────────────────────────────────────────────────────────

export interface HeldTile {
  tileType: HexTileType
  isZombieTile: boolean
}

export interface DeckEntry {
  tileType: HexTileType
  isZombieTile: boolean
  isExitTile: boolean
}

// ── Entities ──────────────────────────────────────────────────────────────────

export interface ZombieToken {
  id: string
  pos: string
}

export interface CharacterState {
  playerId: string
  pos: string | null
  eliminated: boolean
}

export interface PlayerSlot {
  id: string
  displayName: string
  avatarUrl: string | null
  seatIndex: number
}

// ── Round-boundary horde-phase actions ─────────────────────────────────────────

export interface ZombieRoll {
  zombieId: string
  /** Whether the zombie rotated a pipe this round (no dice in the chase model). */
  pipeTurned: boolean
  /** The direction it stepped toward, or -1 if it did not step. */
  direction: number
  moved: boolean
}

// ── Top-level state (mirrors v2 AD-OB-4 state shape) ─────────────────────────

export interface HexEscapeState {
  // Phase / outcome
  phase: Phase
  outcome: Outcome | null

  // Turn / round model
  roundNumber: number
  activeSeat: number | null
  actionPointsRemaining: number
  qualifyingActionsThisTurn: number
  seatsActedThisRound: number[]

  // Exit state
  exitRevealed: boolean
  exitCell: string | null
  exitConnectedCount: number

  // Level metadata
  levelId: string
  levelName: string

  // Board
  cells: string[]
  grid: Record<string, HexCell>

  // Deck / hands (projection masks other players' hands and deck)
  hands: Record<string, HeldTile[]>
  handSizes: Record<string, number>
  deckSize: number

  // Reserved spawn cell per player id
  reservedSpawnCells: Record<string, string>

  // Zombies
  zombies: ZombieToken[]
  lastZombieRolls: ZombieRoll[]

  // Characters (one per player; pos null until first tile placed)
  characters: CharacterState[]

  // Players identity list
  players: PlayerSlot[]

  // Level zone hints (for rendering)
  spawnZoneCells: string[]
  exitZoneCells: string[]
}

// ── Actions dispatched to the server ─────────────────────────────────────────

export type HexEscapeAction =
  | { type: 'DrawTile' }
  | { type: 'PlaceTile'; coord: string; tileType: HexTileType; rotation: number }
  | { type: 'PlaceZombieTile'; coord: string }
  | { type: 'RotateTile'; coord: string; rotation: number }
  | { type: 'MoveCharacter'; coord: string }
  | { type: 'EndTurn' }
