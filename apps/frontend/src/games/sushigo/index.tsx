import type { GameModule } from '@meepliton/contracts'
import type { SushiGoState } from './types'
import SushiGoGame from './components/SushiGoGame'

const sushigo: GameModule<SushiGoState> = {
  gameId: 'sushigo',
  Component: SushiGoGame,
}

export default sushigo
