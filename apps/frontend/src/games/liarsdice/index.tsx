import type { GameModule } from '@meepliton/contracts'
import type { LiarsDiceState } from './types'
import LiarsDiceGame from './components/LiarsDiceGame'

const liarsdice: GameModule<LiarsDiceState> = {
  gameId: 'liarsdice',
  Component: LiarsDiceGame,
}

export default liarsdice
