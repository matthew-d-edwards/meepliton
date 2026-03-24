// Mirror of DeadMansSwitchModels.cs — keep in sync

export type DeadMansSwitchPhase =
  | 'Placing' | 'Bidding' | 'Revealing' | 'DiscardChoice' | 'RoundOver' | 'Finished'

export type DiscType = 'Rose' | 'Skull'

export interface DiscSlot {
  type: DiscType
  flipped: boolean
}

export interface FlipLog {
  flippedByPlayerId: string
  stackOwnerId: string
  result: DiscType
  flipNumber: number
}

export interface DevicePlayer {
  id: string
  displayName: string
  avatarUrl: string | null
  seatIndex: number
  stack: DiscSlot[]       // projected: opponent stacks arrive as []
  stackCount: number      // always accurate; use this for face-down disc count
  rosesOwned: number
  skullOwned: boolean
  pointsWon: number
  active: boolean
  passed: boolean
}

export interface DeadMansSwitchState {
  phase: DeadMansSwitchPhase
  players: DevicePlayer[]
  currentPlayerIndex: number
  currentBid: number
  totalDiscsOnTable: number
  challengerId: string | null
  nextRoundFirstPlayerIndex: number
  lastFlip: FlipLog | null
  winner: string | null
  roundNumber: number
}
