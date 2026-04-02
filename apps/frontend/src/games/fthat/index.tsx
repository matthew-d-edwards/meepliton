import type { GameModule } from '@meepliton/contracts'
import type { FThatView } from './types'
import FThatGame from './components/FThatGame'

const fthat: GameModule<FThatView> = {
  gameId: 'fthat',
  theme: 'fthat',
  Component: FThatGame,
}

export default fthat
