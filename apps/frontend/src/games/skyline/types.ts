// Mirror of SkylineModels.cs — keep in sync

export interface SkylineState {
  players:         PlayerState[]
  board:           (number | null)[][]  // board[row][col]
  currentPlayerId: string
  phase:           'PlacingTile' | 'GameOver'
  turn:            number
  winnerId:        string | null
}

export interface PlayerState {
  id:          string
  displayName: string
  avatarUrl:   string | null
  seatIndex:   number
  score:       number
  hand:        number[]
}

export type SkylineAction =
  | { type: 'PlaceTile'; placeTile: { row: number; col: number; tileValue: number } }
  | { type: 'Undo' }
