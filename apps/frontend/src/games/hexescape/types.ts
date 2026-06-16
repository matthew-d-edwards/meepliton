// Mirror of HexEscapeModels.cs — keep in sync with C# records
// Enums serialize as PascalCase strings (JsonStringEnumConverter on the backend).

export type Phase = 'Playing' | 'GameOver'
export type Outcome = 'Escaped' | 'Overrun'
export type HexTileType = 'Straight' | 'Elbow' | 'Tee' | 'Cross' | 'Deadend'
export type HexActionType = 'PlaceTile' | 'RotateTile' | 'Pass'

export interface HexCell {
  tileType: HexTileType
  rotation: number
  fixed: boolean
}

export interface PlayerSlot {
  id: string
  displayName: string
  avatarUrl: string | null
  seatIndex: number
}

export interface HexEscapeState {
  phase: Phase
  outcome: Outcome | null
  levelId: string
  levelName: string
  /** All valid board cell keys in "q,r" format */
  cells: string[]
  /** Impassable cells */
  walls: string[]
  /** Occupied cells only; key = "q,r" */
  grid: Record<string, HexCell>
  survivorStartCells: string[]
  exitCell: string
  handCounts: Record<HexTileType, number>
  threatCounter: number
  threatThreshold: number
  connectedSurvivors: number
  totalSurvivors: number
  /** Seat indices that have already acted this round */
  seatsActedThisRound: number[]
  players: PlayerSlot[]
}

export type HexEscapeAction =
  | { type: 'PlaceTile'; coord: string; tileType: HexTileType; rotation: number }
  | { type: 'RotateTile'; coord: string; rotation: number }
  | { type: 'Pass' }
