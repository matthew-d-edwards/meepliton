// Mirror of LoveLetterModels.cs — keep in sync
// C# property names serialise to camelCase via JsonNamingPolicy.CamelCase.

export type LoveLetterPhase = 'Waiting' | 'Playing' | 'RoundEnd' | 'GameOver'

export interface LoveLetterState {
  phase:               LoveLetterPhase
  players:             LoveLetterPlayer[]
  deckSize:            number
  faceUpSetAside:      string[]
  currentPlayerIndex:  number
  round:               number
  lastRoundResult:     RoundResult | null
  pendingPriestReveal: PriestReveal | null
  winner:              string | null
}

export interface LoveLetterPlayer {
  id:          string
  displayName: string
  avatarUrl:   string | null
  seatIndex:   number
  handCard:    string | null  // own card visible to self; null for other players
  discardPile: string[]
  tokens:      number
  active:      boolean
  handmaid:    boolean
}

export interface RoundResult {
  winnerIds: string[]
  reason:    string
  reveals:   PlayerHandReveal[]
}

export interface PlayerHandReveal {
  playerId: string
  card:     string | null
}

export interface PriestReveal {
  viewerId: string
  targetId: string
  card:     string
}

export type LoveLetterAction =
  | { type: 'StartGame' }
  | { type: 'PlayCard'; cardPlayed: string; targetId?: string; guessedCard?: string }
  | { type: 'AcknowledgePriest' }
  | { type: 'StartNextRound' }
