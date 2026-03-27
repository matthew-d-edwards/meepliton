// Mirror of ColorettoModels.cs — keep in sync

export type ColorettoPhase = 'Waiting' | 'Playing' | 'Finished'

export interface ColorettoState {
  phase: ColorettoPhase
  players: ColorettoPlayer[]
  deckSize: number
  rows: ColorettoRow[]
  currentPlayerIndex: number
  endGameTriggered: boolean
  finalScores: RoundScoreResult | null
  winner: string | null
}

export interface ColorettoPlayer {
  id: string
  displayName: string
  avatarUrl: string | null
  seatIndex: number
  collection: Record<string, number>  // colour → count
  hasTakenThisRound: boolean
}

export interface ColorettoRow {
  rowIndex: number
  cards: string[]
}

export interface RoundScoreResult {
  scores: PlayerScore[]
}

export interface PlayerScore {
  playerId: string
  collection: Record<string, number>
  topColors: string[]
  colorScores: Record<string, number>
  total: number
}

export type ColorettoAction =
  | { type: 'StartGame' }
  | { type: 'DrawCard'; rowIndex: number }
  | { type: 'TakeRow'; rowIndex: number }
