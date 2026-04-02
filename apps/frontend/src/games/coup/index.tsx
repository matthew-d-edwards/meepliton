import type { GameModule } from '@meepliton/contracts'
import type { CoupState } from './types'
import CoupGame from './components/CoupGame'

const coup: GameModule<CoupState> = {
  gameId: 'coup',
  theme: 'inner-circle',
  Component: CoupGame,
}

export default coup
