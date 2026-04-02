import type { GameModule } from '@meepliton/contracts'
import type { DeadMansSwitchState } from './types'
import DeadMansSwitchGame from './components/DeadMansSwitchGame'

const deadmansswitch: GameModule<DeadMansSwitchState> = {
  gameId: 'deadmansswitch',
  theme: 'deadmansswitch',
  Component: DeadMansSwitchGame,
}

export default deadmansswitch
