import type { GameModule } from '@meepliton/contracts'
import type { HexEscapeState } from './types'
import Game from './components/Game'

const hexescape: GameModule<HexEscapeState> = {
  gameId: 'hexescape',
  theme: 'hexescape',
  Component: Game,
}

export default hexescape
