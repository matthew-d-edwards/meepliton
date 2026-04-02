import type { GameModule } from '@meepliton/contracts'
import type { SkylineState } from './types'
import Game from './components/Game'

const skyline: GameModule<SkylineState> = {
  gameId: 'skyline',
  theme: 'skyline',
  Component: Game,
}

export default skyline
