import type { GameModule } from '@meepliton/contracts'
import type { ColorettoState } from './types'
import ColorettoGame from './components/ColorettoGame'

const coloretto: GameModule<ColorettoState> = {
  gameId: 'coloretto',
  Component: ColorettoGame,
}

export default coloretto
