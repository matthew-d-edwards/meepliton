import type { GameModule } from '@meepliton/contracts'
import type { LoveLetterState } from './types'
import LoveLetterGame from './components/LoveLetterGame'

const loveletter: GameModule<LoveLetterState> = {
  gameId: 'loveletter',
  theme: 'affairs-of-the-court',
  Component: LoveLetterGame,
}

export default loveletter
