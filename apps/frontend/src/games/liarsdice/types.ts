// Mirror of LiarsDiceModels.cs — keep in sync

export type LiarsDicePhase = 'Bidding' | 'Reveal' | 'Finished'

export interface LiarsDiceState {
  phase:               LiarsDicePhase
  players:             DicePlayer[]
  currentPlayerIndex:  number
  currentBid:          Bid | null
  roundNumber:         number
  palificoActive:      boolean
  lastChallengeResult: string | null
  lastReveal:          RevealSnapshot | null
  winner:              string | null
}

export interface DicePlayer {
  id:              string
  displayName:     string
  avatarUrl:       string | null
  seatIndex:       number
  dice:            number[]   // empty array for eliminated players
  diceCount:       number
  active:          boolean
  hasUsedPalifico: boolean
}

export interface Bid {
  quantity: number
  face:     number
}

export interface RevealSnapshot {
  players:       PlayerReveal[]
  challengedBid: Bid
  actualCount:   number
  loserId:       string
}

export interface PlayerReveal {
  playerId: string
  dice:     number[]
}

export type LiarsDiceAction =
  | { type: 'StartGame' }
  | { type: 'PlaceBid';       bid: Bid }
  | { type: 'CallLiar' }
  | { type: 'StartNextRound' }
  | { type: 'DeclarePalifico' }
