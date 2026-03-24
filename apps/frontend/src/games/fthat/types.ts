// Mirror of FThatModels.cs (projected view only) — keep in sync

export type FThatPhase = 'Playing' | 'GameOver'
export type FThatActionType = 'Take' | 'Pass'

export interface FThatView {
  phase: FThatPhase
  players: FThatPlayerView[]
  currentPlayerIndex: number
  deckCount: number
  faceUpCard: number
  chipsOnCard: number
  scores: FThatScore[] | null
  winners: string[] | null
}

export interface FThatPlayerView {
  id: string
  displayName: string
  avatarUrl: string | null
  seatIndex: number
  chips: number        // exact for self; -1 for opponents
  chipsHidden: boolean // false for self; true for opponents
  cards: number[]      // collected cards, always visible
}

export interface FThatScore {
  playerId: string
  cardScore: number
  chips:     number
  total:     number
}
