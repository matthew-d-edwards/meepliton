import type { FC } from 'react'

export interface PlayerInfo {
  id:          string
  displayName: string
  avatarUrl:   string | null
  seatIndex:   number
  connected:   boolean
}

// Everything a game component receives from the platform
export interface GameContext<TState> {
  state:      TState
  players:    PlayerInfo[]
  myPlayerId: string
  roomId:     string
  dispatch:   (action: unknown) => void
}

// A game module is a named React component that renders itself
export interface GameModule<TState = unknown> {
  gameId:    string
  Component: FC<GameContext<TState>>
}
