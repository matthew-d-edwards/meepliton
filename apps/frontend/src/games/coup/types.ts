// Mirror of CoupModels.cs — keep in sync

export type CoupPhase = 'Waiting' | 'AwaitingResponses' | 'InfluenceLoss' | 'Exchange' | 'Finished'

export type PendingStep = 'ActionResponses' | 'BlockResponses'

export interface CoupState {
  phase:             CoupPhase
  players:           CoupPlayer[]
  deck:              string[]
  activePlayerIndex: number
  pending:           PendingAction | null
  winner:            string | null
}

export interface CoupPlayer {
  id:          string
  displayName: string
  avatarUrl:   string | null
  seatIndex:   number
  influence:   InfluenceCard[]
  coins:       number
  active:      boolean
}

export interface InfluenceCard {
  character: string | null  // null = hidden face-down card (other players' unrevealed cards)
  revealed:  boolean
}

export interface PendingAction {
  actionType:             string
  actorId:                string
  targetId:               string | null
  step:                   PendingStep
  passedPlayers:          string[]
  blockerId:              string | null
  challengerId:           string | null
  influenceLossPlayerId:  string | null
  exchangeOptions:        string[] | null
}

export type CoupAction =
  | { type: 'StartGame' }
  | { type: 'TakeIncome' }
  | { type: 'TakeForeignAid' }
  | { type: 'DoCoup'; targetId: string }
  | { type: 'TakeTax' }
  | { type: 'Assassinate'; targetId: string }
  | { type: 'Steal'; targetId: string }
  | { type: 'Exchange' }
  | { type: 'Challenge' }
  | { type: 'Block'; character: string }
  | { type: 'Pass' }
  | { type: 'LoseInfluence'; influenceToLose: string }
  | { type: 'ChooseExchange'; keepCards: string[] }
