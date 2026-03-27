// Mirror of SushiGoModels.cs — keep in sync

export type SushiGoPhase = 'Waiting' | 'Picking' | 'Revealing' | 'Scoring' | 'Finished'

export interface SushiGoState {
  phase:       SushiGoPhase
  players:     SushiGoPlayer[]
  round:       number
  turn:        number
  hands:       string[][]     // only own hand populated; others are []
  handSizes:   number[]       // how many cards each player holds (null in canonical state, populated in projection)
  winner:      string | null
}

export interface SushiGoPlayer {
  id:               string
  displayName:      string
  avatarUrl:        string | null
  seatIndex:        number
  tableau:          string[]
  roundScores:      number[]
  puddingCount:     number
  hasPicked:        boolean
  usingChopsticks:  boolean
}

// Actions mirror the flat C# SushiGoAction record (camelCase on the wire)
export type SushiGoAction =
  | { type: 'StartGame' }
  | { type: 'PickCard'; pick: string }
  | { type: 'UseChopsticks'; pick: string; pick2: string }
  | { type: 'AdvanceRound' }
